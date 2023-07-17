using IEC61850.Client;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                    currentProfileName.Text = value ? Globals.currentProfile.Name.TrimEnd('*') : Globals.currentProfile.Name + "*";
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

        private void populateIedTree()
        {
            iedTree.Items.Clear();

            foreach (IEDConfig ied in Globals.currentProfile.IEDs)
            {
                TreeViewItem iedItem = new TreeViewItem();
                StackPanel iconAndName = new StackPanel();

                iconAndName.Orientation = Orientation.Horizontal;
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
                iedItem.IsExpanded = true;
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

                open.Click += (object sender, RoutedEventArgs e) => Process.Start(ied.logsFolder);

                actions.Items.Add(open);

                iedItem.ContextMenu = actions;

                buildIedFilesTree(ied, iedItem, ied.logsFolder);


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

        public MainWindow()
        {

            InitializeComponent();
            Globals.logs = new Logs(new List<TextBox>()
            {
                this.Logs
            });

            //this.Hide();

            Globals.logs.log("Software started");
            Globals.globalConfiguration = new GlobalConfiguration();
            ConfigFolder.Path = Globals.globalConfiguration.getCurrentConfigFolder();
            initializeFolders(Globals.globalConfiguration.Folder);
            Globals.logs.setFolder(ConfigFolder.extend(ConfigFolder.LOGS));

            Globals.config = new UserConfig(ConfigFolder.Path, (FSyncConfiguration config, FSyncPreferences pref) =>
            {
                resumePollingStartup.IsChecked = config.resumePollingOnStartup;
            });
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
                    this.currentProfileName.Text = "No profile";
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
                    saveProfileButton.IsEnabled = true;
                    deleteProfileButton.IsEnabled = true;
                    //iedName.Visibility = Visibility.Visible;
                    toggleIedButtonsEnabled(true);
                }

                File.WriteAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE), Globals.currentProfile.FilePath);

                Globals.currentProfile.IedsChanged = new Profile.IedsChangedHandler(populateIedTree);

                this.currentProfileName.Text = Globals.currentProfile.Name;
                if (renameOnly)
                {
                    return;
                }
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

            var placeholder = new TreeViewItem();
            placeholder.Header = "No IED Configured";
            iedTree.Items.Add(placeholder);

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

            if (File.Exists(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE)))
            {
                string profilePath = File.ReadAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE));

                if (File.Exists(profilePath))
                {
                    string fileName = Path.GetFileName(profilePath);
                    Globals.currentProfile = new Profile(fileName.Substring(0, fileName.Length - Profile.PROFILEEXTENSION.Length));
                }
            }

            Globals.profileChangeHandler(false);

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

            configuration.cyclePeriod.Text = Globals.config.config.cyclePeriod.ToString();

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
            ied.logsFolder = ConfigFolder.extend(ConfigFolder.IEDLOGSROOT);
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
            }
        }

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
                add.Content = folder.Key;
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
            if (Globals.currentProfile != null && Globals.currentProfile.HasUnsavedChanges)
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
                Globals.currentProfile = new Profile(profileDialog.profileName);
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
                if (Directory.Exists(iedLogFolderInput.Text))
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
                    return LogFolderValidationResult.NOTEXIST;
                }
            }
            catch (Exception)
            {
                return LogFolderValidationResult.NOTEXIST;
            }
        }

        private void updateNameLabel(object sender, TextChangedEventArgs e)
        {
            //iedFieldChanged(sender, e);
            isIedSaved = false;
        }

        private void iedFieldChanged(object sender, TextChangedEventArgs e)
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
                    Globals.currentProfile = null;
                }
            }

        }

        private void handleClosing(object sender, System.ComponentModel.CancelEventArgs e)
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
                            Globals.logs.log("Process idling, closing");
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
        }

        private void fetchIedData(object sender, RoutedEventArgs e)
        {
            IEDConfig selectedIed = getSelectedIed();

            if (selectedIed == null) return;

            fetchDataText.Text = "Fetching data...";

            using (IED ied = new IED(selectedIed))
            {
                if (!ied.connect())
                {
                    MessageBox.Show("Failed to connect to IED. Check the credentials and retry.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    fetchDataText.Text = "Fetch data";
                    return;
                }

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



                List<string> dirTree = ied.ReadFileTree();

                Dictionary<string, bool> extensions = ied.GetAllUsedExtensions(dirTree);
                Dictionary<string, bool> folders = ied.GetAllUsedFolders(dirTree);

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
                    folderBox.Content = folder.Key;
                    folderBox.Tag = folder.Key;
                    folderBox.IsChecked = folder.Value;
                    folderBox.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                    folderBox.Unchecked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                    iedLogFoldersIncludedInput.Items.Add(folderBox);
                }

                this.isIedSaved = false;

                fetchDataText.Text = "Fetch data";

                if (MessageBox.Show("Operation successful, save the IED?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    saveIed(sender, null);
                }

            }
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

        private void iedProcessed_Callback(IED ied, bool status)
        {
            foreach (TreeViewItem iedItem in iedTree.Items)
            {
                if (iedItem.Tag != null && iedItem.Tag is IED && ((IED)iedItem.Tag).config.Equals(ied.config))
                {
                    StackPanel header = (StackPanel)iedItem.Header;
                    TextBlock name = (TextBlock)header.Children[1];
                    name.Foreground = status ? Brushes.Green : Brushes.Red;

                    iedItem.Items.Clear();

                    buildIedFilesTree(ied.config, iedItem, ied.config.logsFolder);

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
                    TextBlock name = (TextBlock)header.Children[1];
                    name.Foreground = Brushes.Black;
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
                    TextBlock name = (TextBlock)header.Children[1];
                    name.Foreground = Brushes.Black;
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

            if ((iedSaved && profileSaved) || configurationModeTab.IsFocused) return;

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

        private void requestLicense(object sender, RoutedEventArgs e)
        {

        }
    }
}
