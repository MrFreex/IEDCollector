using IEC61850.Client;
using IEDCollector.Windows;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    static class Globals
    {
        public const string FOLDERSNAME = "IEDCollector";
        public static OnProfileChange profileChangeHandler = null;
        public static Runner currentProcess = null;
        public static UserConfig config = null;


        public delegate void OnProfileChange(bool renameOnly);

        public static Profile currentProfile
        {
            get { return currProfile; }
            set
            {
                currProfile = value;
                if (profileChangeHandler != null)
                {
                    profileChangeHandler(false);
                }
            }
        }

        private static Profile currProfile;

        public static Logs logs;
        public static GlobalConfiguration globalConfiguration;
    }

    static class ConfigFolder
    {

        private static string folderPath;

        public static string Path { get { return folderPath; } set { setFolderPath(value); } }

        public const string LOGS = "logs";
        public const string LOCALES = "locales";
        public const string PROFILES = "profiles";
        public const string IEDLOGSROOT = "IEDlogs";

        public const string LASTPROFILEFILE = ".lastprofile";

        public static string extend(string constant)
        {
            return System.IO.Path.Combine(folderPath, constant);
        }

        private static void setFolderPath(string folderPath)
        {
            ConfigFolder.folderPath = folderPath;
        }
    }

    class FileData
    {
        private string fileName;

        public FileData(string fileName) { this.fileName = fileName; }

        public string getFileName() { return fileName; }
    }


    public partial class MainWindow : Window
    {
        private bool isIedSaved
        {
            get
            {
                return iedSaved;
            }

            set
            {
                if (((ListBoxItem)iedSelector.SelectedItem) != null)
                {
                    if (value)
                    {
                        //iedName.Text = iedNameInput.Text.TrimEnd('*');
                        //((ListBoxItem)iedSelector.SelectedItem).Content = ((string)(((ListBoxItem)iedSelector.SelectedItem).Content)).TrimEnd('*');
                        foreach (ListBoxItem iedItem in iedSelector.Items)
                        {
                            iedItem.Content = ((IEDConfig)iedItem.Tag).name;
                        }
                    }
                    else
                    {
                        //iedName.Text = iedNameInput.Text + "*";
                        ((ListBoxItem)iedSelector.SelectedItem).Content = ((IEDConfig)((ListBoxItem)iedSelector.SelectedItem).Tag).name + "*";
                    }
                }


                iedSaved = value;
            }
        }
        private bool iedSaved = true;
        private bool isProfileSaved
        {
            get { return profileSaved; }
            set
            {
                if (Globals.currentProfile != null)
                {
                    ComboBoxItem profileItem = (ComboBoxItem)profileSelector.SelectedItem;
                    profileItem.Content = ((Profile)profileItem.Tag).Name + (value ? "" : "*");
                    //currentProfileName.Text = value ? Globals.currentProfile.Name.TrimEnd('*') : Globals.currentProfile.Name + "*";
                }
                profileSaved = value;
            }
        }
        private bool profileSaved = true;

        private bool canExecute = false;

        public bool CanExecute
        {
            set
            {
                canExecute = value;
                startSingle.IsEnabled = value;
                startPolling.IsEnabled = value;
                alreadyClicked = false;
            }

            get
            {
                return canExecute;
            }
        }

        private bool isRunning = false;

        public bool IsRunning
        {
            set
            {
                CanExecute = !value;
                menuOpenConfiguration.IsEnabled = !value;
                menuOpenPreferences.IsEnabled = !value;
                stop.IsEnabled = value;
                configurationModeTab.IsEnabled = !value;
                isRunning = value;
            }

            get
            {
                return isRunning;
            }
        }

        private static readonly string[] NEEDEDFOLDERS =
        {
            ConfigFolder.LOGS,
            ConfigFolder.LOCALES,
            ConfigFolder.PROFILES,
            ConfigFolder.IEDLOGSROOT
        };

        private void iedCheckedUnchecked(IEDConfig c, bool isChecked)
        {
            c.includedInCollection = isChecked;

            saveIeds(null, null);
        }

        private void populateIedTree()
        {
            iedTree.Items.Clear();

            foreach (IEDConfig ied in Globals.currentProfile.IEDs)
            {
                TreeViewItem iedItem = new TreeViewItem();
                StackPanel iconAndName = new StackPanel();

                iconAndName.Orientation = Orientation.Horizontal;
                CheckBox includedInCollection = new CheckBox();
                includedInCollection.IsChecked = ied.includedInCollection;

                includedInCollection.Checked += (object sender, RoutedEventArgs e) =>
                {
                    iedCheckedUnchecked(ied, true);
                };
                includedInCollection.Unchecked += (object sender, RoutedEventArgs e) =>
                {
                    iedCheckedUnchecked(ied, false);
                };

                iconAndName.Children.Add(includedInCollection);
                iconAndName.Children.Add(new Image()
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Icons/hdd-network-fill.png")),
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 5, 0)
                });
                iconAndName.Children.Add(new TextBlock()
                {
                    Text = String.Format("[{0}] {1}", ied.name, ied.ip)
                });

                iedItem.Header = iconAndName;
                iedItem.Tag = new IED(ied);
                //iedItem.IsExpanded = true;
                //iedItem.MouseDoubleClick += (object sender, MouseButtonEventArgs e) => Process.Start(ied.logsFolder);

                ContextMenu actions = new ContextMenu();

                MenuItem open = new MenuItem()
                {
                    Header = "Open",
                    Icon = new Image()
                    {
                        Width = 16,
                        Height = 16,
                        Source = new BitmapImage(new Uri("pack://application:,,,/Icons/folder-fill.png"))
                    }
                };

                open.Click += (object sender, RoutedEventArgs e) => Process.Start(Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.logsFolder));

                actions.Items.Add(open);

                iedItem.ContextMenu = actions;

                iedItem.Items.Add("p");


                iedItem.Expanded += (object sender, RoutedEventArgs e) =>
                {
                    if (iedItem.Items.Count == 1 && iedItem.Items[0] is string)
                    {
                        iedItem.Items.Clear();
                        buildIedFilesTree(ied, iedItem, Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.logsFolder));
                    }
                };



                iedTree.Items.Add(iedItem);
            }


            if (iedTree.Items.Count == 0)
            {
                iedTree.IsEnabled = false;
                iedTree.Items.Add("No IEDs configured");

                this.CanExecute = false;
            }
            else
            {
                iedTree.IsEnabled = true;
                this.CanExecute = true;
            }
        }

        public void buildProfileSelector()
        {
            string[] profiles = Directory.GetFiles(ConfigFolder.extend(ConfigFolder.PROFILES), String.Format("*{0}", Profile.PROFILEEXTENSION));
            profileSelector.Items.Clear();
            foreach (string file in profiles)
            {
                string profileName = Path.GetFileNameWithoutExtension(file);
                ComboBoxItem profile = new ComboBoxItem()
                {
                    Content = profileName,
                    Tag = new Profile(profileName)
                };

                profileSelector.Items.Add(profile);
            }
            profileSelector.IsEnabled = true;
        }

        public void verifyLicense()
        {
            SecurityValidationResult res = Security.validate();

            if (res == SecurityValidationResult.OK) return;

            if (res == SecurityValidationResult.UNSET)
            {
                InsertLicense askForLicense = new InsertLicense();

                askForLicense.Description.Text = "No license found on the computer, please input yours.";

                askForLicense.ShowDialog();

                string license = askForLicense.licenseBox.Password;
                if (Security.validateLicense(license))
                {
                    Security.setLicense(license);
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Globals.logs.log(String.Format("Loading assembly {0}", args.Name), LogLevel.Debug);
            return EmbeddedAssembly.Get(args.Name);
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public static void UnloadImportedDll(string DllPath)
        {
            foreach (System.Diagnostics.ProcessModule mod in System.Diagnostics.Process.GetCurrentProcess().Modules)
            {
                if (mod.FileName.ToUpper() == DllPath.ToUpper())
                {
                    FreeLibrary(mod.BaseAddress);
                }
            }
        }


        public MainWindow()
        {
            verifyLicense();

            InitializeComponent();
            Globals.logs = new Logs(new List<TextBox>()
            {
                this.Logs
            });

            // Tray Icon setup

            NotifyIcon icon = new NotifyIcon();
            Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Icons/IEDCollector.ico")).Stream;
            icon.Icon = new System.Drawing.Icon(iconStream);
            icon.Visible = true;

            icon.Click += (object sender, EventArgs e) =>
            {
                this.Show();
            };

            System.Windows.Forms.MenuItem btn = new System.Windows.Forms.MenuItem()
            {
                Text = "Close"
            };

            btn.Click += (object sender, EventArgs e) =>
            {
                //handleClosing(null, null);
                //Debug.WriteLine("Close " + sender.ToString());
                closeFromTray = true;
                this.Close();
            };

            icon.ContextMenu = new System.Windows.Forms.ContextMenu()
            {
                MenuItems =
                {
                    btn
                }
            };

            //this.Hide();

            Globals.logs.log("Software started");
            Globals.globalConfiguration = new GlobalConfiguration();
            ConfigFolder.Path = Globals.globalConfiguration.getCurrentConfigFolder();
            initializeFolders(Globals.globalConfiguration.Folder);
            Globals.logs.setFolder(ConfigFolder.extend(ConfigFolder.LOGS));

            Globals.config = new UserConfig(ConfigFolder.Path, (FSyncConfiguration config, FSyncPreferences pref) =>
            {
                resumePollingStartup.IsChecked = config.resumePollingOnStartup;
                buildProfileSelector();

            });
            Globals.logs.updateLogLevelSelectors();

            EmbeddedAssembly.Load("IEDCollector.Embed.iec61850.dll", "iec61850.dll");

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            /*
            new Thread(() =>
            {
                IED ied = new IED(new IEDConfig("Test", "10.1.21.211", "", "", 102, "C:\\Users\\FL\\AppData\\Local\\IEDCollector\\IEDlogs\\Test", new Dictionary<string, bool>(), new Dictionary<string, bool>()));
                ied.connect();
                foreach (string entry in ied.ReadFileTree())
                {
                    Globals.logs.log(entry);
                }
            }).Start();

            /*
            new Thread(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    Globals.logs.log("Test " + i);
                    Thread.Sleep(10);
                }
            }).Start();
            */


            Globals.profileChangeHandler = (bool renameOnly) =>
            {

                if (Globals.currentProfile == null)
                {
                    //this.currentProfileName.Text = "No profile";
                    //iedName.Visibility = Visibility.Hidden;
                    connectIedInConf.Visibility = Visibility.Hidden;
                    saveIED.Visibility = Visibility.Hidden;
                    iedSelector.IsEnabled = false;
                    iedSelector.Items.Clear();
                    ListBoxItem add = new ListBoxItem();
                    add.Content = "Load a profile to continue";

                    renameProfileButton.IsEnabled = false;
                    saveProfileButton.IsEnabled = false;
                    deleteProfileButton.IsEnabled = false;
                    exportProfileButton.IsEnabled = false;


                    rootFolderInput.IsEnabled = false;
                    rootFolderInput.Text = "";
                    pollingInterval.IsEnabled = false;
                    pollingInterval.Value = 10;


                    iedSelector.Items.Add(add);
                    iedSettings.Visibility = Visibility.Hidden;
                    toggleIedButtonsEnabled(false);

                    if (File.Exists(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE)))
                    {
                        File.Delete(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE));
                    }

                    return;
                }
                else
                {
                    saveIED.Visibility = Visibility.Visible;
                    connectIedInConf.Visibility = Visibility.Visible;
                    renameProfileButton.IsEnabled = true;
                    exportProfileButton.IsEnabled = true;
                    saveProfileButton.IsEnabled = true;
                    rootFolderInput.IsEnabled = true;
                    pollingInterval.IsEnabled = true;
                    deleteProfileButton.IsEnabled = true;
                    //iedName.Visibility = Visibility.Visible;
                    toggleIedButtonsEnabled(true);
                }

                ComboBoxItem selectedProfile = (ComboBoxItem)profileSelector.SelectedItem;

                if (selectedProfile == null || selectedProfile.Tag == null || !(selectedProfile.Tag is Profile))
                {
                    foreach (ComboBoxItem item in profileSelector.Items)
                    {
                        if (item.Tag is Profile && item.Tag.Equals(Globals.currentProfile))
                        {
                            profileSelector.SelectedItem = item;
                            return;
                        }
                    }
                }

                File.WriteAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE), Globals.currentProfile.FilePath);

                Globals.currentProfile.IedsChanged = new Profile.IedsChangedHandler(populateIedTree);



                //this.currentProfileName.Text = Globals.currentProfile.Name;
                if (renameOnly)
                {
                    ((ComboBoxItem)profileSelector.SelectedItem).Content = Globals.currentProfile.Name;
                    return;
                }

                // Load settings

                rootFolderInput.Text = Globals.currentProfile.Settings.RootFolder;
                pollingInterval.Value = Globals.currentProfile.Settings.PollingInterval;

                // Load Connections

                iedSelector.Items.Clear();
                foreach (IEDConfig ied in Globals.currentProfile.IEDs)
                {
                    ListBoxItem add = new ListBoxItem();
                    add.Content = ied.name;
                    add.Tag = ied;
                    iedSelector.Items.Add(add);
                }

                if (iedSelector.Items.Count > 0)
                {
                    cloneIedBtn.IsEnabled = true;
                    deleteIedBtn.IsEnabled = true;
                    iedSelector.IsEnabled = true;
                    iedSelector.SelectedIndex = 0;
                }
                else
                {
                    cloneIedBtn.IsEnabled = false;
                    deleteIedBtn.IsEnabled = false;
                    ListBoxItem add = new ListBoxItem();
                    add.Content = "No IEDs configured";
                    iedSelector.Items.Add(add);
                    iedSelector.IsEnabled = false;
                }

                if (!IsRunning && File.Exists(ConfigFolder.extend(".resumepolling")))
                {
                    if (Globals.config.config.resumePollingOnStartup)
                    {
                        new Thread(() =>
                        {
                            Thread.Sleep(1000);
                            Application.Current.Dispatcher.Invoke(() => runPolling(null, null));
                        }).Start();

                    }
                    else
                    {
                        try
                        {
                            File.Delete(ConfigFolder.extend(".resumepolling"));
                        }
                        catch { }
                    }
                }
            };

            if (File.Exists(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE)))
            {
                string profilePath = File.ReadAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE));

                if (File.Exists(profilePath))
                {
                    string fileName = Path.GetFileName(profilePath);
                    foreach (ComboBoxItem item in profileSelector.Items)
                    {
                        Profile tag = (Profile)item.Tag;
                        if (tag.Name.Equals(fileName.Substring(0, fileName.Length - Profile.PROFILEEXTENSION.Length)))
                        {
                            profileSelector.SelectedItem = item;
                            break;
                        }
                    }

                }
            }

            Globals.profileChangeHandler(false);

            if (iedTree.Items.Count == 0)
            {
                var placeholder = new TreeViewItem();
                placeholder.Header = "No IED Configured";
                iedTree.Items.Add(placeholder);
            }


            Progress.Value = 0;
            ProgressLabel.Text = "Idling";

            ListBoxItem toAdd = new ListBoxItem();
            toAdd.Content = "No operation queued";
            actionsbox.Items.Add(toAdd);

            // Load Configurations list

            if (iedSelector.Items.Count == 0 || iedSelector.SelectedIndex == -1)
            {
                iedSettings.Visibility = Visibility.Hidden;
            }



            this.KeyDown += (object sender, KeyEventArgs e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if (Keyboard.IsKeyDown(Key.S))
                    {
                        saveIed(sender, e);
                        saveIeds(sender, e);
                    }
                }
            };


        }

        private void toggleIedButtonsEnabled(bool toggle)
        {
            newIedBtn.IsEnabled = toggle;
            cloneIedBtn.IsEnabled = toggle;
            deleteIedBtn.IsEnabled = toggle;
        }

        public static bool receiveF(object param, byte[] data)
        {
            if (param is FileData)
            {
                FileData fd = (FileData)param;
            }
            return true;
        }

        private void initializeFolders(string path)
        {
            foreach (string folder in NEEDEDFOLDERS)
            {
                string combined = Path.Combine(path, folder);
                if (!Directory.Exists(combined))
                {
                    Directory.CreateDirectory(combined);
                }
            }
        }

        private void TestConnection()

        {
            var connection = new IedConnection();
            connection.Connect("10.1.21.201", 102);

            List<string> devices = connection.GetServerDirectory();

            foreach (string device in devices)
            {
                Debug.WriteLine(device);
            }

            /*

            FilesDownloader f = new FilesDownloader("/", (List<string> files) => {
                Debug.WriteLine("Thread Over");
                connection.Abort();
            }, connection);
            */

        }

        private void iedLogFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Select the folder where the logs will be saved";
            dialog.InitialDirectory = this.iedLogFolderInput.Text;
            dialog.ShowNewFolderButton = true;

            Nullable<bool> result = dialog.ShowDialog();

            if (result == true)
            {
                this.iedLogFolderInput.Text = dialog.SelectedPath;
            }
        }

        private void openPreferences(object sender, RoutedEventArgs e)
        {
            Preferences preferences = new Preferences();
            preferences.Show();
        }

        private void openConfiguration(object sender, RoutedEventArgs e)
        {
            //TODO: Add logged in check

            Configuration configuration = new Configuration();

            //configuration.cyclePeriod.Text = Globals.config.config.cyclePeriod.ToString();

            if (Globals.config.config.logFilesKept < 0)
            {
                configuration.keepAllLogFiles.IsChecked = true;
                configuration.logFilesKept.IsEnabled = false;
            }
            else
            {
                configuration.logFilesKept.Text = Globals.config.config.logFilesKept.ToString();
            }

            configuration.startWithWindows.IsChecked = Globals.config.config.startWithWindows;
            configuration.dataLocation.Text = ConfigFolder.Path;

            configuration.Show();
        }

        private void openAbout(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.Show();
        }

        // Configuration view

        private void addIed(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            if (!iedSelector.IsEnabled)
            {
                iedSelector.Items.Clear();
                iedSelector.IsEnabled = true;
            }

            isProfileSaved = false;
            isIedSaved = true;

            IEDConfig ied = new IEDConfig();
            ied.name = "NewIED";
            ied.port = 102;
            ied.ip = "127.0.0.1";
            ied.logsFolder = "";
            ied.logEnabledExtensions = new Dictionary<string, bool>();
            ied.logEnabledFolders = new Dictionary<string, bool>();

            Globals.currentProfile.IEDs.Add(ied);
            ListBoxItem toAdd = new ListBoxItem();
            toAdd.Content = ied.name;
            toAdd.Tag = ied;
            iedSelector.Items.Add(toAdd);
            iedSelector.SelectedIndex = iedSelector.Items.Count - 1;
        }

        private void deleteIed(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null || iedSelector.SelectedIndex == -1)
            {
                return;
            }

            MessageBoxResult res = MessageBox.Show("Are you sure you want to delete this IED?", "Delete IED", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res.Equals(MessageBoxResult.Yes))
            {
                int index = iedSelector.SelectedIndex;
                if (iedSelector.Items.Count > 1)
                {
                    if (index == 0)
                    {
                        iedSelector.SelectedIndex = 1;
                    }
                    else
                    {
                        iedSelector.SelectedIndex -= 1;
                    }
                }
                else
                {
                    ListBoxItem add = new ListBoxItem();
                    add.Content = "No IEDs configured";
                    iedSelector.Items.Add(add);
                    iedSelector.IsEnabled = false;
                }

                Globals.currentProfile.IEDs.RemoveAt(index);
                iedSelector.Items.RemoveAt(index);
                isProfileSaved = false;
            }
        }

        /* OLD, USED WHEN profileSelector didn't exist
        private void loadProfile(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaOpenFileDialog dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;

            dialog.Title = "Select a profile to load";
            dialog.DefaultExt = Profile.PROFILEEXTENSION;
            dialog.Filter = String.Format("Alf profiles (*{0})|*{0}", Profile.PROFILEEXTENSION);
            dialog.InitialDirectory = ConfigFolder.extend(ConfigFolder.PROFILES);
            dialog.ValidateNames = true;
            Nullable<bool> profileSelected = dialog.ShowDialog();

            if (profileSelected != null && (bool)profileSelected)
            {
                Globals.currentProfile = new Profile(Path.GetFileName(dialog.FileName));
                isProfileSaved = true;
            }
        }
        */
        private bool skipAtNext = false;

        private void iedSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (iedSelector.SelectedIndex == -1 && iedSettings.Visibility.Equals(Visibility.Visible))
            {
                iedSettings.Visibility = Visibility.Hidden;
            }
            else if (iedSelector.SelectedIndex >= 0 && iedSettings.Visibility.Equals(Visibility.Hidden))
            {
                iedSettings.Visibility = Visibility.Visible;
            }

            if (skipAtNext)
            {
                skipAtNext = false;
                return;
            }

            if (!isIedSaved)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("Do you want to save the current IED?", "Save IED", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (messageBoxResult.Equals(MessageBoxResult.Yes))
                {
                    saveIed((IEDConfig)((ListBoxItem)e.RemovedItems[0]).Tag);
                }
                else if (messageBoxResult.Equals(MessageBoxResult.Cancel))
                {
                    skipAtNext = true;
                    iedSelector.SelectedItem = e.RemovedItems[0];
                }
            }

            isIedSaved = true;
            IEDConfig selectedIed = getSelectedIed();

            if (selectedIed == null)
            {
                //iedName.Text = "Select a device to continue";
                connectIedInConf.IsEnabled = false;
                saveIED.IsEnabled = false;
                cloneIedBtn.IsEnabled = false;
                deleteIedBtn.IsEnabled = false;
                return;
            };

            saveIED.IsEnabled = true;
            cloneIedBtn.IsEnabled = true;
            deleteIedBtn.IsEnabled = true;
            connectIedInConf.IsEnabled = true;
            //iedName.Text = selectedIed.name;
            iedNameInput.Text = selectedIed.name;
            iedIpInput.Text = selectedIed.ip;

            iedUsernameInput.Text = selectedIed.username;
            iedPasswordInput.Text = selectedIed.password;
            iedPortInput.Text = selectedIed.port.ToString();
            iedLogFolderInput.Text = selectedIed.logsFolder;

            iedLogExtensionsIncludedInput.Items.Clear();
            iedLogFoldersIncludedInput.Items.Clear();

            foreach (KeyValuePair<string, bool> extension in selectedIed.logEnabledExtensions)
            {
                CheckBox add = new CheckBox();
                add.Content = extension.Key;
                add.IsChecked = extension.Value;
                add.Tag = extension.Key;
                add.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                add.Unchecked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                iedLogExtensionsIncludedInput.Items.Add(add);
            }

            foreach (KeyValuePair<string, bool> folder in selectedIed.logEnabledFolders)
            {
                CheckBox add = new CheckBox();
                add.Content = folder.Key.Equals(String.Empty) ? "/" : folder.Key;
                add.IsChecked = folder.Value;
                add.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                add.Unchecked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                add.Tag = folder.Key;
                iedLogFoldersIncludedInput.Items.Add(add);
            }

            isIedSaved = true;
        }

        private void createProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile != null && !isProfileSaved)
            {
                TaskDialog dialog = new TaskDialog();

                dialog.WindowTitle = "Confirm profile creation";
                dialog.Content = "Are you sure you want to create a new profile? All unsaved changes will be lost.";
                dialog.MainInstruction = "Confirm profile creation";
                dialog.MainIcon = TaskDialogIcon.Warning;
                TaskDialogButton autoSave = new TaskDialogButton("Save");
                autoSave.ButtonType = ButtonType.Custom;
                dialog.Buttons.Add(autoSave);
                dialog.Buttons.Add(new TaskDialogButton(Ookii.Dialogs.Wpf.ButtonType.Yes));
                dialog.Buttons.Add(new TaskDialogButton(Ookii.Dialogs.Wpf.ButtonType.No));

                TaskDialogButton result = dialog.ShowDialog();
                if (result != null)
                {
                    if (result.Equals(autoSave))
                    {
                        Globals.currentProfile.save();
                    }
                    else if (result.ButtonType == ButtonType.No)
                    {
                        return;
                    }
                }
            }

            Globals.logs.log("Creating new profile");

            NewProfileDialog profileDialog = new NewProfileDialog();
            Nullable<bool> res = profileDialog.ShowDialog();

            if ((bool)res)
            {
                //Globals.currentProfile = new Profile(profileDialog.profileName);
                ComboBoxItem profile = new ComboBoxItem()
                {
                    Tag = new Profile(profileDialog.profileName),
                    Content = profileDialog.profileName
                };

                profileSelector.Items.Add(profile);
                profileSelector.SelectedItem = profile;
            }
        }

        private void scrollToBottom(object sender, TextChangedEventArgs e)
        {
            Logs.ScrollToEnd();
        }

        private void renameProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            NewProfileDialog newProfileDialog = new NewProfileDialog();
            newProfileDialog.Title = "Rename profile";
            newProfileDialog.ProfileName.Text = Globals.currentProfile.Name;
            newProfileDialog.ok.Content = "Rename";

            Nullable<bool> result = newProfileDialog.ShowDialog();

            if ((bool)result)
            {
                Globals.currentProfile.rename(newProfileDialog.profileName);
                if (Globals.profileChangeHandler != null)
                {
                    Globals.profileChangeHandler(true);
                }
            }
        }

        private void cloneIed(object sender, RoutedEventArgs e)
        {
            // TODO
            IEDConfig selectedIED = getSelectedIed();

            if (selectedIED == null) return;
            IEDConfig cloned = new IEDConfig(selectedIED);
            Globals.currentProfile.IEDs.Add(cloned);
            ListBoxItem add = new ListBoxItem();
            add.Content = cloned.name;
            add.Tag = cloned;
            iedSelector.Items.Add(add);
            iedSelector.SelectedIndex = iedSelector.Items.Count - 1;
        }

        private IEDConfig getSelectedIed()
        {
            if (!iedSelector.IsEnabled) return null;
            if (Globals.currentProfile == null) return null;
            if (Globals.currentProfile.IEDs.Count <= iedSelector.SelectedIndex) return null;
            if (iedSelector.SelectedIndex < 0) return null;

            return Globals.currentProfile.IEDs[iedSelector.SelectedIndex];
        }

        private void saveIed(IEDConfig selectedIed)
        {
            if (selectedIed == null) return;

            if (!checkPortInt())
            {
                MessageBox.Show("Invalid port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IPAddress.Parse(iedIpInput.Text);
            }
            catch
            {
                MessageBox.Show("Invalid IP address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LogFolderValidationResult result = validateLogsPath();

            if (result != LogFolderValidationResult.VALID)
            {
                if (result == LogFolderValidationResult.NOTEXIST)
                {
                    MessageBox.Show("Invalid logs path. The directory path is invalid or doesn't exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("The logs path is being used by another IED.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            ((ListBoxItem)(iedSelector.SelectedItem)).Content = iedNameInput.Text;
            selectedIed.name = iedNameInput.Text;
            selectedIed.ip = iedIpInput.Text;
            selectedIed.username = iedUsernameInput.Text;


            selectedIed.password = iedPasswordInput.Text;
            selectedIed.logsFolder = iedLogFolderInput.Text;

            //selectedIed.logEnabledExtensions.Clear();

            foreach (CheckBox extension in iedLogExtensionsIncludedInput.Items)
            {
                selectedIed.logEnabledExtensions[extension.Tag.ToString()] = (bool)extension.IsChecked;
            }

            selectedIed.logEnabledFolders.Clear();

            foreach (CheckBox folder in iedLogFoldersIncludedInput.Items)
            {
                selectedIed.logEnabledFolders[folder.Tag.ToString()] = (bool)folder.IsChecked;
            }

            isIedSaved = true;
            isProfileSaved = false;
        }

        private void saveIed(object sender, RoutedEventArgs e)
        {
            IEDConfig selectedIed = getSelectedIed();

            saveIed(selectedIed);
        }

        private bool checkPortInt()
        {
            bool issue = false;
            try
            {
                int.Parse(iedPortInput.Text);
            }
            catch
            {
                issue = true;
            }

            if (!issue && int.Parse(iedPortInput.Text) > 65535)
            {
                issue = true;
            }

            return !issue;
        }

        private void checkPortInt(object sender, RoutedEventArgs e)
        {


            if (!checkPortInt())
            {
                MessageBox.Show("Invalid port number", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                iedPortInput.Text = "102";
            }
        }

        private void validateIedIP(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress.Parse(iedIpInput.Text);
            }
            catch
            {
                MessageBox.Show("Invalid IP address", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                iedIpInput.Text = "127.0.0.1";
            }

        }

        private enum LogFolderValidationResult
        {
            VALID,
            USED,
            NOTEXIST
        }

        private LogFolderValidationResult validateLogsPath()
        {
            try
            {
                string path = Path.Combine(Globals.currentProfile.Settings.RootFolder, iedLogFolderInput.Text);
                if (Directory.Exists(path))
                {
                    foreach (IEDConfig config in Globals.currentProfile.IEDs)
                    {
                        if (config.logsFolder.Equals(iedLogFolderInput.Text) && !config.Equals(((ListBoxItem)iedSelector.SelectedItem).Tag))
                        {
                            return LogFolderValidationResult.USED;
                        }
                    }

                    return LogFolderValidationResult.VALID;
                }
                else
                {
                    Directory.CreateDirectory(path);
                    return LogFolderValidationResult.VALID;
                }
            }
            catch (Exception)
            {
                return LogFolderValidationResult.NOTEXIST;
            }
        }

        private string previousConnectionName = "";
        private void updateNameLabel(object sender, TextChangedEventArgs e)
        {
            //iedFieldChanged(sender, e);

            if (iedLogFolderInput.Text.Equals(String.Empty) || iedLogFolderInput.Text.Equals(previousConnectionName))
            {
                iedLogFolderInput.Text = iedNameInput.Text;
            }
            previousConnectionName = iedNameInput.Text;
            isIedSaved = false;
        }

        private void iedFieldChanged(object sender, object e)
        {
            isIedSaved = false;
        }

        private void saveIeds(object sender, RoutedEventArgs e)
        {
            isProfileSaved = true;
            Globals.currentProfile.save();
        }

        private void deleteProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            MessageBoxResult res = MessageBox.Show("Do you really want to delete this profile? The action cannot be undone.", "Delete profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res.Equals(MessageBoxResult.Yes))
            {
                if (Globals.currentProfile.delete())
                {
                    profileSelector.Items.Remove(profileSelector.SelectedItem);

                }
            }

        }

        private bool closeFromTray = false;

        private void handleClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
            //Debug.WriteLine(e == null);
            if (closeFromTray) // Right click on tray icon -> exit
            {
                if (Globals.currentProcess != null && Globals.currentProcess.IsRunning)
                {


                    if (Globals.currentProcess.IsIdling)
                    {
                        Globals.currentProcess.Abort();
                    }
                    else
                    {
                        Globals.logs.log("Waiting for current process to idle before closing");
                        new Thread(() =>
                        {
                            Globals.currentProcess.Dispose();

                            while (Globals.currentProcess.Worker.IsAlive)
                            {
                                Thread.Sleep(10);
                            }

                            Globals.currentProcess.Abort();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Globals.logs.log("Process idling, closing", LogLevel.Detailed);
                                Application.Current.Shutdown();
                            });
                        })
                        { IsBackground = true }.Start();

                        e.Cancel = true;
                        return;
                    }

                    
                }

                if (!isProfileSaved || !isIedSaved)
                {
                    MessageBoxResult res = MessageBox.Show("Do you want to save changes?", "Save changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res.Equals(MessageBoxResult.Yes))
                    {
                        saveIed(sender, null);
                        saveIeds(sender, null);
                    }
                    else if (res.Equals(MessageBoxResult.Cancel))
                    {
                        e.Cancel = true;
                    }
                }
            } else
            {
                this.Hide();
                e.Cancel = true;
            }
            
        }

        private void fetchIedData(object sender, RoutedEventArgs e)
        {
            IEDConfig selectedIed = getSelectedIed();

            if (selectedIed == null) return;

            fetchDataText.Text = "Fetching data...";
            ProgressDialog progress = new ProgressDialog();

            progress.ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar;
            progress.Text = "Connecting to IED...";
            progress.WindowTitle = "Fetch IED data";
            progress.ShowCancelButton = true;
            CancellationTokenSource source = new CancellationTokenSource();
            //progress.MinimizeBox = true;
            progress.Show(source.Token);

            bool success = true;

            progress.DoWork += (object sender2, DoWorkEventArgs e2) =>
            {


                using (IED ied = new IED(selectedIed))
                {
                    if (!ied.connect())
                    {
                        MessageBox.Show("Failed to connect to IED. Check the credentials and retry.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        success = false;
                        return;
                    }

                    if (source.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    //progress.Text = "Performing device name cross-check...";

                    try
                    {
                        if (!ied.CrossCheckName())
                        {
                            MessageBoxResult askIfContinue = MessageBox.Show("The configured name differs from the one found inside the IED. Continue anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            if (askIfContinue != MessageBoxResult.Yes)
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error reading the device directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    if (source.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    //progress.Text = "Reading folders and extensions...";

                    List<string> dirTree = ied.ReadFileTree();

                    if (source.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    Dictionary<string, bool> extensions = ied.GetAllUsedExtensions(dirTree);
                    Dictionary<string, bool> folders = ied.GetAllUsedFolders(dirTree);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        selectedIed.logEnabledExtensions = extensions;
                        selectedIed.logEnabledFolders = folders;

                        iedLogExtensionsIncludedInput.Items.Clear();
                        iedLogFoldersIncludedInput.Items.Clear();

                        foreach (KeyValuePair<string, bool> extension in extensions)
                        {
                            CheckBox extensionBox = new CheckBox();
                            extensionBox.Content = extension.Key;
                            extensionBox.Tag = extension.Key;
                            extensionBox.IsChecked = extension.Value;
                            extensionBox.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                            extensionBox.Unchecked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                            iedLogExtensionsIncludedInput.Items.Add(extensionBox);
                        }

                        foreach (KeyValuePair<string, bool> folder in folders)
                        {
                            CheckBox folderBox = new CheckBox();
                            folderBox.Content = folder.Key.Equals(String.Empty) ? "/" : folder.Key;
                            folderBox.Tag = folder.Key;
                            folderBox.IsChecked = folder.Value;
                            folderBox.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                            folderBox.Unchecked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                            iedLogFoldersIncludedInput.Items.Add(folderBox);
                        }

                        this.isIedSaved = false;

                        fetchDataText.Text = "Fetch data";

                    }, System.Windows.Threading.DispatcherPriority.Render);
                }
            };

            progress.RunWorkerCompleted += (object sender2, RunWorkerCompletedEventArgs e2) =>
            {
                fetchDataText.Text = "Fetch data";
                if (success && MessageBox.Show("Operation successful, save the IED?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    saveIed(sender, null);
                }
            };
        }

        private void buildIedFilesTree(IEDConfig ied, TreeViewItem iedItem, string path)
        {
            try
            {
                foreach (string directory in Directory.GetDirectories(path))
                {
                    TreeViewItem directoryItem = new TreeViewItem();
                    StackPanel iconAndName = new StackPanel();

                    iconAndName.Orientation = Orientation.Horizontal;
                    iconAndName.Children.Add(new Image()
                    {
                        Source = new BitmapImage(new Uri("pack://application:,,,/Icons/folder-fill.png")),
                        Width = 16,
                        Height = 16,

                        Margin = new Thickness(0, 0, 5, 0)
                    });
                    iconAndName.Children.Add(new TextBlock()
                    {
                        Text = String.Format("{0}", directory.Substring(directory.LastIndexOf('\\')).Replace("\\", "").Replace("/", "")),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    directoryItem.Header = iconAndName;

                    //directoryItem.MouseDoubleClick += (object sender, MouseButtonEventArgs e) => Process.Start(directory);

                    ContextMenu actions = new ContextMenu();

                    MenuItem open = new MenuItem()
                    {
                        Header = "Open",
                        Icon = new Image()
                        {
                            Width = 16,
                            Height = 16,
                            Source = new BitmapImage(new Uri("pack://application:,,,/Icons/folder-fill.png"))
                        }
                    };

                    open.Click += (object sender, RoutedEventArgs e) => Process.Start(directory);

                    actions.Items.Add(open);

                    directoryItem.ContextMenu = actions;


                    buildIedFilesTree(ied, directoryItem, directory);

                    directoryItem.Tag = directory;

                    iedItem.Items.Add(directoryItem);

                }
            }
            catch (IOException)
            {
                Globals.logs.log(String.Format("Directory {0} not found", path));
                return;
            }


            foreach (string file in Directory.GetFiles(path))
            {
                TreeViewItem directoryItem = new TreeViewItem();
                StackPanel iconAndName = new StackPanel();

                iconAndName.Orientation = Orientation.Horizontal;
                iconAndName.Children.Add(new Image()
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Icons/file-plus-fill.png")),
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 2.5, 5, 2.5)
                });
                iconAndName.Children.Add(new TextBlock()
                {
                    Text = String.Format("{0}", Path.GetFileName(file))
                });

                directoryItem.Header = iconAndName;
                //directoryItem.MouseDoubleClick += (object sender, MouseButtonEventArgs e) => Process.Start(file);

                ContextMenu actions = new ContextMenu();

                MenuItem open = new MenuItem()
                {
                    Header = "Open",
                    Icon = new Image()
                    {
                        Width = 16,
                        Height = 16,
                        Source = new BitmapImage(new Uri("pack://application:,,,/Icons/folder-fill.png"))
                    }
                };

                open.Click += (object sender, RoutedEventArgs e) => Process.Start(file);

                actions.Items.Add(open);

                MenuItem properties = new MenuItem()
                {
                    Header = "Properties",
                    Icon = new Image()
                    {
                        Width = 16,
                        Height = 16,
                        Source = new BitmapImage(new Uri("pack://application:,,,/Icons/wrench-adjustable.png"))
                    }
                };

                properties.Click += (object sender, RoutedEventArgs e) =>
                {
                    openFileMetadata(ied, file.Replace(ied.logsFolder, ""));
                };

                actions.Items.Add(properties);

                directoryItem.ContextMenu = actions;



                iedItem.Items.Add(directoryItem);

            }
        }

        private void openFileMetadata(IEDConfig iedConf, string file)
        {
            IED ied = new IED(iedConf);

            string fileNoSlashes = file.TrimStart('\\');

            new Thread(() =>
            {
                EditableFileDirectoryEntry fileEntry = null;
                if (ied.connect())
                {
                    Dictionary<EditableFileDirectoryEntry, bool> remoteReducedTree = ied.ReadFileTree(Path.GetDirectoryName(file).TrimStart('\\'), true);
                    foreach (KeyValuePair<EditableFileDirectoryEntry, bool> entry in remoteReducedTree)
                    {
                        Debug.WriteLine(String.Format("{0} {1} {2}", fileNoSlashes, entry.Key.fileName, file));
                        if (entry.Key.fileName.EndsWith(fileNoSlashes) || entry.Key.fileName.EndsWith(fileNoSlashes.Replace("\\", "/")))
                        {
                            fileEntry = entry.Key;
                            break;
                        }
                    }

                    if (fileEntry != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            //FileMetadataWindow metadataWindow = new FileMetadataWindow(ied, fileEntry);
                            //metadataWindow.Show();
                            FileMetadata windows = new FileMetadata();

                            file = Path.Combine(iedConf.logsFolder, fileNoSlashes);
                            FileInfo fileInfo = new FileInfo(file);


                            windows.localPathBox.Text = fileNoSlashes;
                            windows.localSizeBox.Text = fileInfo.Length.ToString() + " byte";
                            windows.localModifiedBox.Text = File.GetLastWriteTime(file).ToLongTimeString();
                            windows.localHashCodeBox.Text = fileInfo.GetHashCode().ToString();

                            windows.remotePathBox.Text = fileEntry.fileName;
                            Debug.WriteLine(fileEntry.lastModified);
                            windows.remoteModifiedBox.Text = DateTimeOffset.FromUnixTimeMilliseconds((long)fileEntry.lastModified).DateTime.ToLongTimeString();
                            windows.remoteSizeBox.Text = fileEntry.fileSize.ToString() + " byte";
                            windows.remoteHashCodeBox.Text = fileEntry.GetHashCode().ToString();

                            windows.Show();
                        });
                    }
                    else
                    {
                        MessageBox.Show("File not found on the IED", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Could not connect to the IED", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }).Start();



        }

        private void iedProcessed_Callback(IED ied, ExecutionResult status)
        {
            foreach (TreeViewItem iedItem in iedTree.Items)
            {
                if (iedItem.Tag != null && iedItem.Tag is IED && ((IED)iedItem.Tag).config.Equals(ied.config))
                {
                    StackPanel header = (StackPanel)iedItem.Header;
                    Image icon = (Image)header.Children[1];
                    //name.Foreground = status ? Brushes.Green : Brushes.Red;

                    icon.Source = new BitmapImage(new Uri((status == ExecutionResult.SUCCESS || status == ExecutionResult.SKIPPED) ? "pack://application:,,,/Icons/hdd-network-fill-green.png" : "pack://application:,,,/Icons/hdd-network-fill-red.png"));



                    if (status == ExecutionResult.SUCCESS || status == ExecutionResult.PARTIAL)
                    {
                        iedItem.Items.Clear();
                        buildIedFilesTree(ied.config, iedItem, Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.config.logsFolder));
                    }

                    break;
                }
            }




        }

        private void runSingle(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            foreach (TreeViewItem ied in iedTree.Items)
            {
                if (ied.Tag != null)
                {
                    StackPanel header = (StackPanel)ied.Header;
                    Image icon = (Image)header.Children[1];
                    icon.Source = new BitmapImage(new Uri("pack://application:,,,/Icons/hdd-network-fill.png"));
                    //name.Foreground = Brushes.Black;
                }
            }

            IsRunning = true;
            CanExecute = false;

            Runner runner = new Runner(() =>
            {
                IsRunning = false;
            }, RunType.SINGLE, Globals.currentProfile.IEDs, Progress, FileProgress, FileName, actionsbox, ProgressLabel, iedProcessed_Callback);

            runner.Start();



            Globals.currentProcess = runner;
        }



        private void runPolling(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            foreach (TreeViewItem ied in iedTree.Items)
            {
                if (ied.Tag != null)
                {
                    StackPanel header = (StackPanel)ied.Header;
                    Image icon = (Image)header.Children[1];
                    icon.Source = new BitmapImage(new Uri("pack://application:,,,/Icons/hdd-network-fill.png"));
                }
            }

            this.IsRunning = true;
            CanExecute = false;

            Runner runner = new Runner(() =>
            {
                this.IsRunning = false;
                Progress.IsIndeterminate = false;
                Progress.Value = 0;
                actionsbox.Items.Clear();
            }, RunType.POLLING, Globals.currentProfile.IEDs, Progress, FileProgress, FileName, actionsbox, ProgressLabel, iedProcessed_Callback);



            try
            {
                if (Globals.config.config.resumePollingOnStartup)
                {
                    File.WriteAllText(ConfigFolder.extend(".resumepolling"), "");
                }
            }
            catch { Globals.logs.log("[WARNING] Couldn't write resumepolling file, the polling won't resume on startup."); }

            Debug.WriteLine(IsRunning);

            runner.Start();





            Globals.currentProcess = runner;
        }

        private bool alreadyClicked = false;
        private void Stop(object sender, RoutedEventArgs e)
        {
            Globals.logs.log("Stopping execution");

            if (File.Exists(ConfigFolder.extend(".resumepolling")))
            {
                try
                {
                    File.Delete(ConfigFolder.extend(".resumepolling"));
                }
                catch
                {
                    Globals.logs.log("[WARNING] Couldn't delete resumepolling file, the polling will resume on startup.");
                }
            }

            if (Globals.currentProcess != null)
            {
                Debug.WriteLine(alreadyClicked);
                if (!alreadyClicked && Globals.currentProcess.IsRunning)
                {
                    alreadyClicked = true;
                    Globals.currentProcess.Dispose();
                    return;
                }
                else
                {
                    if (Globals.currentProcess.Worker.IsAlive)
                    {
                        if (MessageBox.Show("Forcefully terminate the process?", "Abort process", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            Globals.logs.log("Stopping execution FORCEFULLY (User)");
                            Globals.currentProcess.Worker.Abort();
                            Progress.IsIndeterminate = false;
                            Progress.Value = 0;
                            actionsbox.Items.Clear();
                            this.IsRunning = false;
                            ProgressLabel.Text = "Idling";
                        }
                    }
                }
            }
        }

        private void tabChangeHandler(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count == 0) return;

            if (e.RemovedItems[0].Equals(viewerModeTab) && IsRunning)
            {
                MessageBox.Show("You cannot configure the software while it's running", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                tabControl.SelectedIndex = 0;
                return;
            }

            if ((iedSaved && profileSaved) || !e.RemovedItems[0].Equals(configurationModeTab)) return;

            if (MessageBox.Show("Save changes?", "Save changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                saveIed(sender, null);
                saveIeds(sender, null);
            }
        }

        private void resumePollingStartup_Checked(object sender, RoutedEventArgs e)
        {
            if (Globals.config != null && Globals.config.config.resumePollingOnStartup != resumePollingStartup.IsChecked)
            {
                Globals.config.config.resumePollingOnStartup = (bool)resumePollingStartup.IsChecked;
                Globals.config.save();
            }
        }

        private void profileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 && e.RemovedItems.Count > 0) Globals.currentProfile = null;
            if (e.AddedItems.Count == 0 || !profileSelector.IsEnabled) return;

            if (!isIedSaved || !isProfileSaved)
            {
                TaskDialog askToSave = new TaskDialog();
                askToSave.MainIcon = TaskDialogIcon.Warning;
                askToSave.WindowTitle = "Save changes";
                askToSave.MainInstruction = "Would you like to save the changes you made?";
                askToSave.Buttons.Add(new TaskDialogButton(ButtonType.Yes));
                askToSave.Buttons.Add(new TaskDialogButton(ButtonType.No));
                askToSave.Buttons.Add(new TaskDialogButton(ButtonType.Cancel));
                TaskDialogButton result = askToSave.ShowDialog();
                if (result.ButtonType.Equals(ButtonType.Yes))
                {
                    saveIed(null, null);
                    saveIeds(null, null);
                }
                else if (result.ButtonType.Equals(ButtonType.Cancel))
                {
                    profileSelector.IsEnabled = false;
                    profileSelector.SelectedItem = e.RemovedItems[0];
                    profileSelector.IsEnabled = true;
                    return;
                }

                ((ComboBoxItem)e.RemovedItems[0]).Content = Globals.currentProfile.Name;

                isProfileSaved = true;
                isIedSaved = true;
            }

            ComboBoxItem profileCombo = (ComboBoxItem)e.AddedItems[0];
            Profile selected = (Profile)profileCombo.Tag;

            Globals.currentProfile = selected;
        }

        private void GenericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text, @"[^a-zA-Z-_0-9]");
        }

        private static bool IsTextAllowed(string Text, string AllowedRegex)
        {
            try
            {
                var regex = new Regex(AllowedRegex);
                return !regex.IsMatch(Text);
            }
            catch
            {
                return true;
            }
        }

        private void ipFormatting(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text, @"[^0-9\.]");
        }

        private void profileFieldChanged(object sender, TextChangedEventArgs e)
        {
            if (Globals.currentProfile == null || rootFolderInput.Text == Globals.currentProfile.Settings.RootFolder) return;
            isProfileSaved = false;
        }

        private void profileFieldChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Globals.currentProfile == null || pollingInterval.Value == Globals.currentProfile.Settings.PollingInterval) return;
            isProfileSaved = false;
        }

        private void BrowseProfileRootFolder(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();

            dialog.InitialDirectory = ConfigFolder.extend(ConfigFolder.IEDLOGSROOT);
            dialog.ShowNewFolderButton = true;
            dialog.Multiselect = false;
            dialog.Description = "Select the local root folder";

            bool? result = dialog.ShowDialog();

            if (result != null && (bool)result)
            {
                if (Directory.Exists(dialog.SelectedPath))
                {
                    foreach (ComboBoxItem profileItem in profileSelector.Items)
                    {
                        Profile profile = (Profile)profileItem.Tag;

                        if (profile != Globals.currentProfile && profile.Settings.RootFolder.Equals(dialog.SelectedPath))
                        {
                            MessageBox.Show("This root folder is already in use by another profile", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    rootFolderInput.Text = dialog.SelectedPath;
                }
            }
        }

        private void exportProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            VistaSaveFileDialog dialog = new VistaSaveFileDialog()
            {
                CheckFileExists = false,
                FileName = Globals.currentProfile.Name,
                AddExtension = true,
                DefaultExt = Profile.PROFILEEXTENSION,
                Filter = String.Format("Profile files (*{0})|*{0}", Profile.PROFILEEXTENSION)
            };

            bool? res = dialog.ShowDialog();

            if (res != null && (bool)res)
            {
                File.Copy(Globals.currentProfile.FilePath, dialog.FileName, true);
                MessageBox.Show("Profile exported successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void importProfile(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog dialog = new VistaOpenFileDialog()
            {
                CheckFileExists = true,
                //FileName = Globals.currentProfile.Name,
                //AddExtension = true,
                DefaultExt = Profile.PROFILEEXTENSION,
                Filter = String.Format("Profile files (*{0})|*{0}", Profile.PROFILEEXTENSION)
            };

            bool? res = dialog.ShowDialog();

            if (res != null && (bool)res)
            {
                if (!isProfileSaved)
                {
                    MessageBoxResult r = MessageBox.Show("Current profile unsaved. Save changes?", "Save changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    if (r.Equals(MessageBoxResult.Yes))
                    {
                        saveIed(null, null);
                        saveIeds(null, null);
                    }
                    else if (r.Equals(MessageBoxResult.Cancel))
                    {
                        return;
                    }
                }

                try
                {
                    File.Copy(dialog.FileName, Path.Combine(ConfigFolder.extend(ConfigFolder.PROFILES), Path.GetFileName(dialog.FileName)));
                    Profile loaded = new Profile(Path.GetFileNameWithoutExtension(dialog.FileName));

                    ComboBoxItem item = new ComboBoxItem()
                    {
                        Content = loaded.Name,
                        Tag = loaded
                    };

                    profileSelector.Items.Add(item);

                    profileSelector.SelectedItem = item;
                }
                catch (Exception)
                {
                    MessageBox.Show("The profile already exists, rename it before proceeding.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void openLicenseWindow(object sender, RoutedEventArgs e)
        {
            InsertLicense window = new InsertLicense();

            window.Description.Text = "License details";

            window.ShowDialog();

            if (Application.Current == null) return;

            if (Security.validate() != SecurityValidationResult.OK) Application.Current.Shutdown();
        }
    }
}
