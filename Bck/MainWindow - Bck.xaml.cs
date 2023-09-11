using IEC61850.Client;
using IEDCollector.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

//Internal imports
using IEDCollector.Services.Configuration;

namespace IEDCollectorBck
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private bool profileSaved = true;
        private bool canExecute = false;
        private bool isRunning = false;
        private bool iedSaved = true;

        private bool IsIedSaved
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
        private bool IsProfileSaved
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
        public bool IsRunning
        {
            set
            {
                CanExecute = !value;
                menuOpenConfiguration.IsEnabled = !value;
                menuOpenPreferences.IsEnabled = !value;
                openLicenseButton.IsEnabled = !value;
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

        // Entry point
        public MainWindow()
        {
            //initCulture();

            if (!verifyLicense()) return;

            InitializeComponent(); // Load all the WPF components

            Application.Current.SessionEnding += (object sender, SessionEndingCancelEventArgs e) => // handle watchdog restart
            {
                closeFromTray = true;
                handleClosing(sender, e);
            };

            this.Title = Globals.IsFreeMode ? this.Title + " - Free Mode" : this.Title;

            Globals.logs = new Logs(new List<TextBox>() // Initialize the logs object, no logs are allowed before this line
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
                Text = Properties.Resources.close
            };

            btn.Click += (object sender, EventArgs e) =>
            {
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



            Globals.logs.log("Software started");

            // Load the options.xml file in the programData root folder, it contains the data path for each user

            Globals.globalConfiguration = new GlobalConfiguration();
            ConfigFolder.Path = Globals.globalConfiguration.getCurrentConfigFolder();

            initializeFolders(Globals.globalConfiguration.Folder); // Create the needed folders if they do not exist
            Globals.logs.setFolder(ConfigFolder.extend(ConfigFolder.LOGS)); // Set logs folder

            Globals.config = new UserConfig(ConfigFolder.Path, (config, pref) =>
            {
                resumePollingStartup.IsChecked = config.resumePollingOnStartup;
                buildProfileSelector();

            });

            Globals.logs.updateLogLevelSelectors();

            EmbeddedAssembly.Load("IEDCollector.Embed.iec61850.dll", "iec61850.dll");

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            bool startingAfterPolling = false;

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
                    add.Content = Properties.Resources.load_profile_to_continue;

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

                File.WriteAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE), Globals.currentProfile.FilePath); // Save the last loaded profile to file so we can automatically load it on next start

                Globals.currentProfile.IedsChanged = new Profile.IedsChangedHandler(populateIedTree);

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

                    addIedContextMenu(add);

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
                    add.Content = Properties.Resources.no_connections_configured;
                    iedSelector.Items.Add(add);
                    iedSelector.IsEnabled = false;
                }

                if (!IsRunning && !startingAfterPolling && File.Exists(ConfigFolder.extend(".resumepolling")))
                {
                    if (Globals.config.config.resumePollingOnStartup)
                    {
                        startingAfterPolling = true;
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

            // Set the selected profile to the last one loaded
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
                placeholder.Header = Properties.Resources.no_connections_configured;
                iedTree.Items.Add(placeholder);
            }


            Progress.Value = 0;
            ProgressLabel.Text = Properties.Resources.idling;

            ListBoxItem toAdd = new ListBoxItem();
            toAdd.Content = Properties.Resources.no_operation_queued;
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
                        saveProfile(sender, e);
                    }
                }
            };


        }

        // Event handler for the checkboxes in the iedTree
        private void iedCheckedUnchecked(IEDConfig c, bool isChecked)
        {
            c.includedInCollection = isChecked;

            saveProfile(null, null);
        }

        //bool stress = false;

        // Responsible for loading the list of IEDs from the profile and loading their file structure
        // The file structure is taken from the local folders and hence refers to the last fetched one.
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
                    Source = (BitmapImage)FindResource("HddIcon"),
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
                    Header = Properties.Resources.open,
                    Icon = new Image()
                    {
                        Width = 16,
                        Height = 16,
                        Source = (BitmapImage)FindResource("FolderIcon")
                    }
                };

                open.Click += (object sender, RoutedEventArgs e) => Process.Start(Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.logsFolder));

                actions.Items.Add(open);

                iedItem.ContextMenu = actions;

                iedItem.Items.Add("p");


                iedItem.Expanded += (object sender, RoutedEventArgs e) =>
                {
                    // Debug stress test
                    /*
                    if (!stress)
                    {
                        stress = true;
                        new Thread(() =>
                        {
                            while (true)
                            {
                                Application.Current.Dispatcher.Invoke(() => iedItem.IsExpanded = false);
                                Thread.Sleep(500);
                                Application.Current.Dispatcher.Invoke(() => iedItem.IsExpanded = true);
                                Thread.Sleep(500);
                            }
                        }).Start();
                    }
                    */

                    if (iedItem.Items.Count == 1 && iedItem.Items[0] is string)
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                        iedItem.Items.Clear();
                        buildIedFilesTree(ied, iedItem, Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.logsFolder));
                        Mouse.OverrideCursor = null;
                    }
                };

                iedItem.Collapsed += (object sender, RoutedEventArgs e) =>
                {
                    if (!e.Source.Equals(iedItem)) return;

                    // Clean the fuck out of the memory 
                    iedItem.Items.Clear();

                    new Thread(() =>
                    {
                        Thread.Sleep(1000);
                        Application.Current.Dispatcher.Invoke(() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true));
                    }).Start();

                    iedItem.Items.Add("p");
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

        // Adds the profile items to the selector
        public void buildProfileSelector()
        {
            string[] profiles = Directory.GetFiles(ConfigFolder.extend(ConfigFolder.PROFILES), String.Format("*{0}", Profile.PROFILEEXTENSION));
            profileSelector.Items.Clear();
            profileSelector.IsEnabled = false;
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

        // On application startup, it is responsible to determine whether the user has a license or not.
        // It also handles the "Switch to free mode" button.
        // @return : true if the user has a valid license or opted for the free version, false otherwise.
        public bool verifyLicense()
        {

            SecurityValidationResult res = Security.validate();

            if (Security.IsFreeMode)
            {
                Globals.IsFreeMode = true;
                return true;
            }

            Globals.IsFreeMode = false;

            if (res == SecurityValidationResult.OK) return true;

            if (res == SecurityValidationResult.UNSET || res == SecurityValidationResult.INVALID)
            {
                InsertLicense askForLicense = new InsertLicense();

                //askForLicense.Description.Text = Properties.Resources.no_license_found_on_computer;

                askForLicense.removeLicenseButton.IsEnabled = false;

                askForLicense.ShowDialog();

                if (Security.IsFreeMode)
                {
                    Globals.IsFreeMode = true;
                    return true;
                }

                try
                {
                    string license = File.ReadAllText(askForLicense.licenseBox.Text);

                    if (Security.validateLicense(license))
                    {
                        Security.setLicense(license);
                        MessageBox.Show(Properties.Resources.messagebox_activation_successful, Properties.Resources.success, MessageBoxButton.OK);
                        return true;
                    }
                    else
                    {
                        Application.Current.Shutdown();
                        return false;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
            }

            return false;
        }

        // Taken from IEDExplorer, helps resolve the iec61850 dlls
        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Globals.logs.log(String.Format("Loading assembly {0}", args.Name), LogLevel.Debug);
            return EmbeddedAssembly.Get(args.Name);
        }

        public void MoveItem(int direction, ListBoxItem item)
        {
            // Checking selected item

            // Calculate new index using move direction
            int newIndex = iedSelector.Items.IndexOf(item) + direction;

            // Checking bounds of the range
            if (newIndex < 0 || newIndex >= iedSelector.Items.Count)
                return; // Index out of range - nothing to do

            ListBoxItem selected = item;

            // Removing removable element
            iedSelector.Items.Remove(selected);
            Globals.currentProfile.IEDs.Remove((IEDConfig)selected.Tag);
            // Insert it in new position

            Globals.currentProfile.IEDs.Insert(newIndex, (IEDConfig)selected.Tag);
            iedSelector.Items.Insert(newIndex, selected);
            // Restore selection
            //iedSelector.SetSelected(newIndex, true);

            IsProfileSaved = false;
        }

        private void addIedContextMenu(ListBoxItem add)
        {
            add.ContextMenu = new ContextMenu();
            add.ContextMenu.Items.Add(new MenuItem()
            {
                Header = Properties.Resources.ied_move_up,
                Tag = "up"
            });
            add.ContextMenu.Items.Add(new MenuItem()
            {
                Header = Properties.Resources.ied_move_down,
                Tag = "down"
            });

            foreach (MenuItem item in add.ContextMenu.Items)
            {
                item.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (item.Tag.Equals("up"))
                    {
                        MoveItem(-1, add);
                    }
                    else
                    {
                        MoveItem(1, add);
                    }
                };
            }


        }

        

        private void toggleIedButtonsEnabled(bool toggle)
        {
            newIedBtn.IsEnabled = toggle;
            cloneIedBtn.IsEnabled = toggle;
            deleteIedBtn.IsEnabled = toggle;
        }

        // Initializes all the needed folders in the user data folder
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

        // Opens the preferences window

        private void openPreferences(object sender, RoutedEventArgs e)
        {
            Preferences preferences = new Preferences();
            preferences.Show();
        }

        // Opens the configuration window

        private void openConfiguration(object sender, RoutedEventArgs e)
        {
            //TODO: Add logged in check (v2)

            Configuration configuration = new Configuration();

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
            configuration.minimizeToTray.IsChecked = Globals.config.config.minimizeToTray;
            configuration.dataLocation.Text = ConfigFolder.Path;

            configuration.Show();
        }

        // Opens the about window

        private void openAbout(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.Show();
        }

        // Adds a connection to the connection list in the current profile

        private void addIed(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            if (!iedSelector.IsEnabled)
            {
                iedSelector.Items.Clear();
                iedSelector.IsEnabled = true;
            }

            IsProfileSaved = false;
            IsIedSaved = true;

            IEDConfig ied = new IEDConfig();
            ied.name = Properties.Resources.new_ied;
            ied.username = "";
            ied.port = 102;
            ied.ip = "127.0.0.1";
            ied.logsFolder = ied.name;
            ied.logEnabledExtensions = new Dictionary<string, bool>();
            ied.logEnabledFolders = new Dictionary<string, bool>();



            Globals.currentProfile.IEDs.Add(ied);
            ListBoxItem toAdd = new ListBoxItem();

            addIedContextMenu(toAdd);

            toAdd.Content = ied.name;
            toAdd.Tag = ied;
            iedSelector.Items.Add(toAdd);
            iedSelector.SelectedIndex = iedSelector.Items.Count - 1;
        }

        // Removes a connection in the connection list in the current profile

        private void deleteIed(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null || iedSelector.SelectedIndex == -1)
            {
                return;
            }

            MessageBoxResult res = MessageBox.Show(Properties.Resources.messagebox_confirm_delete_connection, Properties.Resources.messagebox_confirm_delete_connection_title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

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
                    add.Content = Properties.Resources.no_connections_configured;
                    iedSelector.Items.Add(add);
                    iedSelector.IsEnabled = false;
                }

                Globals.currentProfile.IEDs.RemoveAt(index);
                iedSelector.Items.RemoveAt(index);
                IsProfileSaved = false;
            }
        }

        
        private bool skipAtNext = false;

        // Called when the user changes the selected connection

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

            if (!IsIedSaved)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(Properties.Resources.messagebox_save_connection, Properties.Resources.messagebox_save_connection_title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
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

            IsIedSaved = true;
            IEDConfig selectedIed = getSelectedIed();

            if (selectedIed == null)
            {
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
                add.Checked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                add.Unchecked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                iedLogExtensionsIncludedInput.Items.Add(add);
            }

            foreach (KeyValuePair<string, bool> folder in selectedIed.logEnabledFolders)
            {
                CheckBox add = new CheckBox();
                add.Content = folder.Key.Equals(String.Empty) ? "/" : folder.Key;
                add.IsChecked = folder.Value;
                add.Checked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                add.Unchecked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                add.Tag = folder.Key;
                iedLogFoldersIncludedInput.Items.Add(add);
            }

            IsIedSaved = true;
        }

        // Called on the click of the "Create Profile" button

        private void createProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile != null && !IsProfileSaved)
            {
                TaskDialog dialog = new TaskDialog();

                dialog.WindowTitle = Properties.Resources.confirm_profile_creation_title;
                dialog.Content = Properties.Resources.confirm_profile_creation;
                dialog.MainInstruction = Properties.Resources.confirm_profile_creation_instruction;
                dialog.MainIcon = TaskDialogIcon.Warning;
                TaskDialogButton autoSave = new TaskDialogButton(Properties.Resources.save);
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

        // When a new log is generated, this method is called

        private void scrollToBottom(object sender, TextChangedEventArgs e)
        {
            Logs.ScrollToEnd();
        }

        // Called when the "Rename profile" button is pressed

        private void renameProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            NewProfileDialog newProfileDialog = new NewProfileDialog();
            newProfileDialog.Title = Properties.Resources.rename_profile;
            newProfileDialog.ProfileName.Text = Globals.currentProfile.Name;
            newProfileDialog.ok.Content = Properties.Resources.rename;

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

        // Called when the "Clone connection" button is pressed

        private void cloneIed(object sender, RoutedEventArgs e)
        {
            // TODO
            IEDConfig selectedIED = getSelectedIed();

            if (selectedIED == null) return;
            IEDConfig cloned = new IEDConfig(selectedIED, true);
            Globals.currentProfile.IEDs.Add(cloned);
            ListBoxItem add = new ListBoxItem();

            addIedContextMenu(add);

            add.Content = cloned.name;
            add.Tag = cloned;
            iedSelector.Items.Add(add);
            iedSelector.SelectedIndex = iedSelector.Items.Count - 1;
        }

        // Utility: retrieves the currently selected connection

        private IEDConfig getSelectedIed()
        {
            if (!iedSelector.IsEnabled) return null;
            if (Globals.currentProfile == null) return null;
            if (Globals.currentProfile.IEDs.Count <= iedSelector.SelectedIndex) return null;
            if (iedSelector.SelectedIndex < 0) return null;

            return Globals.currentProfile.IEDs[iedSelector.SelectedIndex];
        }

        private List<T> copyToList<T>(ItemCollection items)
        {
            List<T> ret = new List<T>();

            foreach (T item in items)
            {
                ret.Add(item);
            }

            return ret;
        }

        // Saves the given connection to the profile

        private void saveIed(IEDConfig selectedIed)
        {
            if (selectedIed == null) return;

            if (!checkPortInt())
            {
                MessageBox.Show(Properties.Resources.messagebox_invalid_port, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IPAddress.Parse(iedIpInput.Text);
            }
            catch
            {
                MessageBox.Show(Properties.Resources.messagebox_invalid_ip, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LogFolderValidationResult result = validateLogsPath();

            if (result != LogFolderValidationResult.VALID)
            {
                if (result == LogFolderValidationResult.NOTEXIST)
                {
                    MessageBox.Show(Properties.Resources.messagebox_invalid_logs_path, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(Properties.Resources.messagebox_logs_path_used, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
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
            selectedIed.logEnabledExtensions.Clear();

            List<CheckBox> extensionsCopy = copyToList<CheckBox>(iedLogExtensionsIncludedInput.Items);

            foreach (CheckBox extension in extensionsCopy)
            {
                CheckBox final = extension;
                if (extension.Tag == null)
                {
                    final = checkBoxInputToExtension(extension);
                }

                selectedIed.logEnabledExtensions[final.Tag.ToString()] = (bool)final.IsChecked;
            }

            selectedIed.logEnabledFolders.Clear();

            List<CheckBox> foldersCopy = copyToList<CheckBox>(iedLogFoldersIncludedInput.Items);

            foreach (CheckBox folder in foldersCopy)
            {
                CheckBox final = folder;
                if (folder.Tag == null)
                    final = checkBoxInputToFolder(folder);

                selectedIed.logEnabledFolders[final.Tag.ToString()] = (bool)final.IsChecked;
            }

            IsIedSaved = true;
            IsProfileSaved = false;
        }

        // Called when the "Save connection" button is pressed

        private void saveIed(object sender, RoutedEventArgs e)
        {
            IEDConfig selectedIed = getSelectedIed();

            saveIed(selectedIed);
        }

        // Validates the port number

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

        // Validates the IP address input

        private void validateIedIP(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress.Parse(iedIpInput.Text);
            }
            catch
            {
                MessageBox.Show(Properties.Resources.messagebox_invalid_ip, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Warning);
                iedIpInput.Text = "127.0.0.1";
            }

        }

        private enum LogFolderValidationResult
        {
            VALID,
            USED,
            NOTEXIST
        }

        // Validates the logs path input

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

        // Changes the logs path input to the connection name if it is empty or matches the previous name
        private void updateNameLabel(object sender, TextChangedEventArgs e)
        {
            //iedFieldChanged(sender, e);

            if (iedLogFolderInput.Text.Equals(String.Empty) || iedLogFolderInput.Text.Equals(previousConnectionName))
            {
                iedLogFolderInput.Text = iedNameInput.Text;
            }
            previousConnectionName = iedNameInput.Text;
            IsIedSaved = false;
        }

        // Updates the bool "isIedSaved" to match the field status
        private void iedFieldChanged(object sender, object e)
        {
            IsIedSaved = false;
        }

        // Called when the "Save profile" button is pressed

        private void saveProfile(object sender, RoutedEventArgs e)
        {
            IsProfileSaved = true;

            Globals.currentProfile.Settings.RootFolder = rootFolderInput.Text;
            Globals.currentProfile.Settings.PollingInterval = pollingInterval.Value == null ? 10 : (int)pollingInterval.Value;

            Dictionary<string, bool> usedNames = new Dictionary<string, bool>();
            bool anyName = false;
            // Check for duplicate names

            foreach (IEDConfig config in Globals.currentProfile.IEDs)
            {
                if (usedNames.ContainsKey(config.name))
                {
                    usedNames[config.name] = true;
                    anyName = true;
                }
                else
                {
                    usedNames.Add(config.name, false);
                }
            }

            if (anyName)
            {
                List<string> duplicates = new List<string>();
                foreach (KeyValuePair<string, bool> pair in usedNames)
                {
                    if (pair.Value)
                    {
                        duplicates.Add(pair.Key);
                    }
                }

                MessageBox.Show(String.Format(Properties.Resources.messagebox_duplicate_ied_name, String.Join(", ", duplicates)), Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check for duplicate folders

            Dictionary<string, List<string>> usedFolders = new Dictionary<string, List<string>>();
            bool anyFolder = false;

            foreach (IEDConfig config in Globals.currentProfile.IEDs)
            {
                if (!usedFolders.ContainsKey(config.logsFolder))
                {
                    usedFolders.Add(config.logsFolder, new List<string>() { config.name });

                }
                else
                {
                    usedFolders[config.logsFolder].Add(config.name);
                    anyFolder = true;
                }
            }

            if (anyFolder)
            {
                Dictionary<string, List<string>> onlyDuplicates = usedFolders.Where(pair => pair.Value.Count > 1).ToDictionary(pair => pair.Key, pair => pair.Value);
                List<string> duplicatePairsForMessage = new List<string>();

                foreach (KeyValuePair<string, List<string>> pair in onlyDuplicates)
                {
                    duplicatePairsForMessage.Add(String.Format("{0} : {1}", pair.Key, String.Join(", ", pair.Value)));
                }

                MessageBox.Show(String.Format(Properties.Resources.messagebox_duplicate_ied_folders, String.Join(";", duplicatePairsForMessage)), Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            Globals.currentProfile.save();
        }

        // Called when the "Delete profile" button is pressed

        private void deleteProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            MessageBoxResult res = MessageBox.Show(Properties.Resources.messagebox_confirm_delete_profile, Properties.Resources.messagebox_confirm_delete_profile_title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res.Equals(MessageBoxResult.Yes))
            {
                if (Globals.currentProfile.delete())
                {
                    profileSelector.Items.Remove(profileSelector.SelectedItem);

                }
            }

        }

        private bool closeFromTray = false;

        // Window closing event handler
        // Close from window's "X" button -> minimize to tray
        // Close from tray icon / rebooting -> exit

        private void handleClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            //Debug.WriteLine(e == null);
            if (!Globals.config.config.minimizeToTray || closeFromTray) // Right click on tray icon -> exit
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

                if (!IsProfileSaved || !IsIedSaved)
                {
                    MessageBoxResult res = MessageBox.Show(Properties.Resources.messagebox_save_changes, Properties.Resources.messagebox_save_changes_title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res.Equals(MessageBoxResult.Yes))
                    {
                        saveIed(sender, null);
                        saveProfile(sender, null);
                    }
                    else if (res.Equals(MessageBoxResult.Cancel))
                    {
                        e.Cancel = true;
                    }
                }
            }
            else
            {

                if (!File.Exists(ConfigFolder.extend(".warned_about_tray")))
                {
                    MessageBox.Show(Properties.Resources.messagebox_tray_warning, Properties.Resources.warning, MessageBoxButton.OK, MessageBoxImage.Information);
                    File.WriteAllText(ConfigFolder.extend(".warned_about_tray"), "true");
                }

                Globals.logs.log("Minimizing to tray", LogLevel.Basic);
                this.Hide();
                e.Cancel = true;
            }

        }

        // Called when the "Fetch Data" button is pressed

        private void fetchIedData(object sender, RoutedEventArgs e)
        {

            IEDConfig selectedIed = getSelectedIed();
            if (selectedIed == null) return;



            fetchDataText.Text = Properties.Resources.fetching_data;
            ProgressDialog progress = new ProgressDialog();

            progress.ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar;
            progress.Text = Properties.Resources.connecting_ied;
            progress.WindowTitle = Properties.Resources.fetching_data_title;
            progress.ShowCancelButton = true;
            CancellationTokenSource source = new CancellationTokenSource();
            //progress.MinimizeBox = true;


            bool success = true;

            progress.DoWork += (object sender2, DoWorkEventArgs e2) =>
            {


                using (IED ied = new IED(selectedIed))
                {
                    IedClientError connectionResult = ied.connect();
                    if (connectionResult != IedClientError.IED_ERROR_OK)
                    {
                        MessageBox.Show(String.Format(connectionResult == IedClientError.IED_ERROR_CONNECTION_REJECTED ? Properties.Resources.ied_error_refused : (connectionResult == IedClientError.IED_ERROR_TIMEOUT ? Properties.Resources.ied_error_offline : Properties.Resources.messagebox_ied_connection_failed), connectionResult.ToString()), Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);

                        success = false;
                        return;
                    }

                    if (source.Token.IsCancellationRequested)
                    {
                        return;
                    }


                    try
                    {
                        string deviceName = ied.CrossCheckName();
                        if (deviceName != ied.config.name)
                        {
                            //MessageBoxResult askIfContinue = MessageBox.Show(Properties.Resources.messagebox_crosscheck_differs, Properties.Resources.warning, MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            //if (askIfContinue != MessageBoxResult.Yes)
                            //{
                            //    return;
                            //}

                            TaskDialogButton useFoundName = new TaskDialogButton(Properties.Resources.button_update_configured_name);
                            TaskDialogButton keepName = new TaskDialogButton(Properties.Resources.button_keep_configured_name);

                            TaskDialog dialog = new TaskDialog()
                            {
                                WindowTitle = Properties.Resources.warning,
                                MainInstruction = Properties.Resources.warning,
                                Content = String.Format(Properties.Resources.messagebox_crosscheck_differs, deviceName, ied.config.name),
                                Buttons =
                                {
                                    useFoundName,
                                    keepName
                                }
                            };

                            if (dialog.ShowDialog() == useFoundName)
                            {
                                ied.config.name = deviceName;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    iedNameInput.Text = deviceName;
                                });
                            }
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(Properties.Resources.messagebox_error_reading_device_dir, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    if (source.Token.IsCancellationRequested)
                    {
                        return;
                    }


                    List<string> dirTree = ied.ReadFileTree();

                    if (source.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    Dictionary<string, bool> extensions = ied.GetAllUsedExtensions(dirTree);
                    Dictionary<string, bool> folders = ied.GetAllUsedFolders(dirTree);

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        selectedIed.logEnabledExtensions = extensions;
                        selectedIed.logEnabledFolders = folders;

                        //iedLogExtensionsIncludedInput.Items.Clear();
                        //iedLogFoldersIncludedInput.Items.Clear();

                        foreach (CheckBox item in iedLogExtensionsIncludedInput.Items)
                        {
                            if (item.Tag != null && extensions.ContainsKey((string)(item.Tag)))
                            {
                                extensions.Remove((string)(item.Tag));
                            }
                        }

                        foreach (KeyValuePair<string, bool> extension in extensions)
                        {
                            CheckBox extensionBox = new CheckBox();
                            extensionBox.Content = extension.Key;
                            extensionBox.Tag = extension.Key;
                            extensionBox.IsChecked = extension.Value;
                            extensionBox.Checked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                            extensionBox.Unchecked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                            iedLogExtensionsIncludedInput.Items.Add(extensionBox);
                        }

                        foreach (CheckBox item in iedLogFoldersIncludedInput.Items)
                        {
                            if (item.Tag != null && folders.ContainsKey((string)(item.Tag)))
                            {
                                folders.Remove((string)(item.Tag));
                            }
                        }

                        foreach (KeyValuePair<string, bool> folder in folders)
                        {
                            CheckBox folderBox = new CheckBox();
                            folderBox.Content = folder.Key.Equals(String.Empty) ? "/" : folder.Key;
                            folderBox.Tag = folder.Key;
                            folderBox.IsChecked = folder.Value;
                            folderBox.Checked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                            folderBox.Unchecked += (object r, RoutedEventArgs args) => { IsIedSaved = false; };
                            iedLogFoldersIncludedInput.Items.Add(folderBox);
                        }

                        this.IsIedSaved = false;

                        fetchDataText.Text = Properties.Resources.fetch_data;

                        if (success && MessageBox.Show(Properties.Resources.messagebox_success_save_connection, Properties.Resources.success, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            saveIed(sender, null);
                        }

                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            };

            progress.RunWorkerCompleted += (object sender2, RunWorkerCompletedEventArgs e2) =>
            {
                fetchDataText.Text = Properties.Resources.fetch_data;
            };

            progress.Show(source.Token);
        }

        // Generates the files and folders of the IEDs in the iedTree
        // ! RECURSIVE !
        private void buildIedFilesTree(IEDConfig ied, TreeViewItem iedItem, string path)
        {
            try
            {
                foreach (string directory in Directory.GetDirectories(path))
                {
                    TreeViewItem directoryItem = new TreeViewItem();
                    StackPanel iconAndName = new StackPanel
                    {
                        Orientation = Orientation.Horizontal
                    };

                    iconAndName.Children.Add(new Image()
                    {
                        Source = (BitmapImage)FindResource("FolderIcon"),
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
                        Header = Properties.Resources.open,
                        Icon = new Image()
                        {
                            Width = 16,
                            Height = 16,
                            Source = (BitmapImage)FindResource("FolderIcon")
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
                    Source = (BitmapImage)FindResource("FileIcon"),
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
                    Header = Properties.Resources.open,
                    Icon = new Image()
                    {
                        Width = 16,
                        Height = 16,
                        Source = (BitmapImage)FindResource("FolderIcon")
                    }
                };

                open.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (Globals.IsFreeMode)
                    {
                        MessageBox.Show(Properties.Resources.messagebox_buy_full_version, Properties.Resources.messagebox_buy_full_version_title, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    Process.Start(file);
                };

                actions.Items.Add(open);

                MenuItem properties = new MenuItem()
                {
                    Header = Properties.Resources.properties,
                    Icon = new Image()
                    {
                        Width = 16,
                        Height = 16,
                        Source = (BitmapImage)FindResource("HddIcon")
                    }
                };

                properties.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (Globals.IsFreeMode)
                    {
                        MessageBox.Show(Properties.Resources.messagebox_buy_full_version, Properties.Resources.messagebox_buy_full_version_title, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    openFileMetadata(ied, file);
                };

                actions.Items.Add(properties);

                directoryItem.ContextMenu = actions;



                iedItem.Items.Add(directoryItem);

                if (Globals.IsFreeMode)
                {
                    File.Delete(file);
                }
            }
        }

        static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        // Called on right click -> properties on a file in the iedTree

        private void openFileMetadata(IEDConfig iedConf, string file)
        {
            IED ied = new IED(iedConf);


            string remoteFile = file.Replace(Path.Combine(Globals.currentProfile.Settings.RootFolder, iedConf.logsFolder), "");
            string fileNoSlashes = remoteFile.TrimStart('\\');

            new Thread(() =>
            {
                EditableFileDirectoryEntry fileEntry = null;
                string fileHash = null;
                IedClientError connectionResult = ied.connect();

                IedClientError error = connectionResult;

                if (connectionResult == IedClientError.IED_ERROR_OK)
                {
                    Dictionary<EditableFileDirectoryEntry, bool> remoteReducedTree = ied.ReadFileTree("", true); //
                    foreach (KeyValuePair<EditableFileDirectoryEntry, bool> entry in remoteReducedTree)
                    {
                        Debug.WriteLine(String.Format("{0} {1} {2}", fileNoSlashes, entry.Key.fileName, file));
                        if (entry.Key.fileName.EndsWith(fileNoSlashes) || entry.Key.fileName.EndsWith(fileNoSlashes.Replace("\\", "/")))
                        {
                            fileEntry = entry.Key;
                            ied.DownloadFile(fileEntry, ConfigFolder.extend(".tmpfile"), new FileProgressMonitor((double prog) => { }), true, true);

                            fileHash = CalculateMD5(ConfigFolder.extend(".tmpfile"));
                            try
                            {
                                File.Delete(ConfigFolder.extend(".tmpfile"));
                            }
                            catch (Exception e) { Globals.logs.log("Error: can't delete .tmpfile. Exception: " + e.ToString(), LogLevel.Detailed); }
                            break;
                        }
                    }

                    if (fileEntry == null)
                    {
                        error = IedClientError.IED_ERROR_OBJECT_DOES_NOT_EXIST;
                    }
                }
                string message = "";

                if (error != IedClientError.IED_ERROR_OK)
                {


                    switch (error)
                    {
                        case IedClientError.IED_ERROR_TIMEOUT: message = Properties.Resources.metadata_window_timeout_error; break;
                        case IedClientError.IED_ERROR_OBJECT_DOES_NOT_EXIST: message = Properties.Resources.metadata_window_file_doesntexist_error; break;
                        default: message = Properties.Resources.metadata_window_not_connected_error; break;
                    }

                    if (!MessageBox.Show(String.Format(Properties.Resources.messagebox_metadata_window_error, message), Properties.Resources.warning, MessageBoxButton.OKCancel, MessageBoxImage.Warning).Equals(MessageBoxResult.OK))
                    {
                        return;
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    //FileMetadataWindow metadataWindow = new FileMetadataWindow(ied, fileEntry);
                    //metadataWindow.Show();
                    FileInfo fileInfo = new FileInfo(file);
                    MetadataWindowFileData remoteFileData;
                    if (error == IedClientError.IED_ERROR_OK)
                    {
                        remoteFileData = new MetadataWindowFileData(new Dictionary<string, string>() {
                                { "Size", fileEntry.fileSize.ToString() + " byte" },
                                        { "Last Modified", DateTimeOffset.FromUnixTimeMilliseconds((long)fileEntry.lastModified).DateTime.ToString("u").Replace(" ", "T") },
                                        { "Hash", fileHash }
                        }, fileEntry.fileName);
                    }
                    else
                    {
                        remoteFileData = new MetadataWindowFileData(new Dictionary<string, string>()
                        {
                            {  "File not accessible", message }
                        }, Properties.Resources.metadata_window_remote_file_unaccessible);
                    }
                    FileMetadata windows = new FileMetadata(new MetadataWindowFileData(new Dictionary<string, string>()
                            {
                                { "Size", fileInfo.Length.ToString() + " byte" },
                                { "Last Modified", File.GetLastWriteTime(file).ToString("u").Replace(" ", "T") },
                                { "Hash", CalculateMD5(file) }
                            }, file),
                            remoteFileData

                    );

                    windows.Show();
                });

                /*
                if (connectionResult == IedClientError.IED_ERROR_OK)
                {
                    //Globals.logs.log("Reading " + Path.GetDirectoryName(file)/*.TrimStart('\\'), LogLevel.Debug);
                    Dictionary<EditableFileDirectoryEntry, bool> remoteReducedTree = ied.ReadFileTree("", true); //
                    foreach (KeyValuePair<EditableFileDirectoryEntry, bool> entry in remoteReducedTree)
                    {
                        Debug.WriteLine(String.Format("{0} {1} {2}", fileNoSlashes, entry.Key.fileName, file));
                        if (entry.Key.fileName.EndsWith(fileNoSlashes) || entry.Key.fileName.EndsWith(fileNoSlashes.Replace("\\", "/")))
                        {
                            fileEntry = entry.Key;
                            ied.DownloadFile(fileEntry, ConfigFolder.extend(".tmpfile"), new FileProgressMonitor((double prog) => { }), true);

                            fileHash = CalculateMD5(ConfigFolder.extend(".tmpfile"));
                            try
                            {
                                File.Delete(ConfigFolder.extend(".tmpfile"));
                            } catch (Exception e) { Globals.logs.log("Error: can't delete .tmpfile. Exception: " + e.ToString(), LogLevel.Detailed); }
                            break;
                        }
                    }

                    if (fileEntry != null) // 
                    {
                        
                    }
                    else
                    {
                        MessageBox.Show(Properties.Resources.messagebox_file_not_found, Properties.Resources.warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show(String.Format(Properties.Resources.messagebox_ied_connection_failed, connectionResult.ToString()), Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                */
            }).Start();



        }

        private static BitmapImage greenIcon = new BitmapImage(new Uri("pack://application:,,,/Icons/hdd-network-fill-green.png"));
        private static BitmapImage redIcon = new BitmapImage(new Uri("pack://application:,,,/Icons/hdd-network-fill-red.png"));

        // Callback : called when the Runner finishes processing one IED

        private void iedProcessed_Callback(IED ied, ExecutionResult status)
        {
            foreach (TreeViewItem iedItem in iedTree.Items)
            {
                if (iedItem.Tag != null && iedItem.Tag is IED && ((IED)iedItem.Tag).config.Equals(ied.config))
                {
                    StackPanel header = (StackPanel)iedItem.Header;
                    Image icon = (Image)header.Children[1];
                    //name.Foreground = status ? Brushes.Green : Brushes.Red;

                    icon.Source = status == ExecutionResult.SUCCESS || status == ExecutionResult.SKIPPED ? greenIcon : redIcon;



                    if (status == ExecutionResult.SUCCESS || status == ExecutionResult.PARTIAL)
                    {
                        if (iedItem.IsExpanded)
                        {
                            iedItem.Items.Clear();
                            buildIedFilesTree(ied.config, iedItem, Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.config.logsFolder));
                        }
                    }

                    break;
                }
            }




        }

        // Called when the single execution "play" button is pressed

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

        // Called when the cyclic execution "arrows" button is pressed

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

        // Called when the stop button is pressed

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
                        if (MessageBox.Show(Properties.Resources.messagebox_confirm_forced_termination, Properties.Resources.messagebox_confirm_forced_termination_title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            Globals.logs.log("Stopping execution FORCEFULLY (User)");
                            Globals.currentProcess.Worker.Abort();
                            Progress.IsIndeterminate = false;
                            Progress.Value = 0;
                            actionsbox.Items.Clear();
                            this.IsRunning = false;
                            ProgressLabel.Text = Properties.Resources.idling;
                        }
                    }
                }
            }
        }

        // Handles the user trying to switch between the viewer and configuration tabs

        private void tabChangeHandler(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count == 0) return;

            if (e.RemovedItems[0].Equals(viewerModeTab) && IsRunning)
            {
                MessageBox.Show(Properties.Resources.messagebox_cannot_configure_while_running, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                tabControl.SelectedIndex = 0;
                return;
            }

            if ((iedSaved && profileSaved) || !e.RemovedItems[0].Equals(configurationModeTab)) return;

            if (MessageBox.Show(Properties.Resources.messagebox_save_changes, Properties.Resources.messagebox_save_changes_title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                saveIed(sender, null);
                saveProfile(sender, null);
            }
        }

        // Saves the "resumePolling" setting

        private void resumePollingStartup_Checked(object sender, RoutedEventArgs e)
        {
            if (Globals.config != null && Globals.config.config.resumePollingOnStartup != resumePollingStartup.IsChecked)
            {
                Globals.config.config.resumePollingOnStartup = (bool)resumePollingStartup.IsChecked;
                Globals.config.save();
            }
        }

        // Changes the loaded profile when the combobox selection varies

        private void profileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 && e.RemovedItems.Count > 0) Globals.currentProfile = null;
            if (e.AddedItems.Count == 0 || !profileSelector.IsEnabled) return;

            if (!IsIedSaved || !IsProfileSaved)
            {
                TaskDialog askToSave = new TaskDialog();
                askToSave.MainIcon = TaskDialogIcon.Warning;
                askToSave.WindowTitle = Properties.Resources.messagebox_save_changes_title;
                askToSave.MainInstruction = Properties.Resources.messagebox_save_changes;
                askToSave.Buttons.Add(new TaskDialogButton(ButtonType.Yes));
                askToSave.Buttons.Add(new TaskDialogButton(ButtonType.No));
                askToSave.Buttons.Add(new TaskDialogButton(ButtonType.Cancel));
                TaskDialogButton result = askToSave.ShowDialog();
                if (result.ButtonType.Equals(ButtonType.Yes))
                {
                    saveIed(null, null);
                    saveProfile(null, null);
                }
                else if (result.ButtonType.Equals(ButtonType.Cancel))
                {
                    profileSelector.IsEnabled = false;
                    profileSelector.SelectedItem = e.RemovedItems[0];
                    profileSelector.IsEnabled = true;
                    return;
                }

                ((ComboBoxItem)e.RemovedItems[0]).Content = Globals.currentProfile.Name;

                IsProfileSaved = true;
                IsIedSaved = true;
            }

            ComboBoxItem profileCombo = (ComboBoxItem)e.AddedItems[0];
            Profile selected = (Profile)profileCombo.Tag;

            Globals.currentProfile = selected;
        }

        // Generic input validation (connection name)

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

        // Validates the ip address field on real time

        private void ipFormatting(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text, @"[^0-9\.]");
        }

        // Tells the software the profile is not saved when its root folder or polling interval field varies

        private void profileFieldChanged(object sender, TextChangedEventArgs e)
        {
            if (Globals.currentProfile == null || rootFolderInput.Text == Globals.currentProfile.Settings.RootFolder) return;
            IsProfileSaved = false;
        }

        // Tells the software the profile is not saved when its root folder or polling interval field varies

        private void profileFieldChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Globals.currentProfile == null || pollingInterval.Value == Globals.currentProfile.Settings.PollingInterval) return;
            IsProfileSaved = false;
        }

        // Called when the "Browse" button is clicked in the profile root folder selection

        private void BrowseProfileRootFolder(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();

            dialog.InitialDirectory = ConfigFolder.extend(ConfigFolder.IEDLOGSROOT);
            dialog.ShowNewFolderButton = true;
            dialog.Multiselect = false;
            dialog.Description = Properties.Resources.select_local_root_folder;

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
                            MessageBox.Show(Properties.Resources.messagebox_root_folder_in_use, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    rootFolderInput.Text = dialog.SelectedPath;
                }
            }
        }

        // Called when the "Export profile" button is pressed

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
                MessageBox.Show(Properties.Resources.messagebox_profile_exported_successfully, Properties.Resources.success, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Called when the "import profile" button is pressed

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
                if (!IsProfileSaved)
                {
                    MessageBoxResult r = MessageBox.Show(Properties.Resources.messagebox_save_changes, Properties.Resources.messagebox_save_changes_title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    if (r.Equals(MessageBoxResult.Yes))
                    {
                        saveIed(null, null);
                        saveProfile(null, null);
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
                    MessageBox.Show(Properties.Resources.messagebox_profile_already_exists, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Opens the license management window

        private void openLicenseWindow(object sender, RoutedEventArgs e)
        {
            bool freeMode = Security.IsFreeMode;
            InsertLicense window = new InsertLicense();

            //window.Description.Text = Properties.Resources.license_details;

            window.freeModeButton.IsEnabled = false;

            window.ShowDialog();

            if (Application.Current == null) return;

            if (!Security.IsFreeMode && Security.validate() != SecurityValidationResult.OK) Application.Current.Shutdown();
            if (freeMode && !Security.IsFreeMode)
            {
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }
        }

        private void addFileExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            TextBox input = new TextBox()
            {
                Text = ".new extension",

            };

            CheckBox item = new CheckBox()
            {
                Content = input,
                IsChecked = true
            };

            input.KeyDown += (object s, KeyEventArgs ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    checkBoxInputToExtension(item);
                }
            };

            iedLogExtensionsIncludedInput.Items.Add(item);
        }

        private void delFileExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            if (iedLogExtensionsIncludedInput.SelectedItem == null) return;

            iedLogExtensionsIncludedInput.Items.Remove(iedLogExtensionsIncludedInput.SelectedItem);
            IsIedSaved = false;
        }

        private CheckBox checkBoxInputToFolder(CheckBox item)
        {
            TextBox input = (TextBox)item.Content;
            string extension = input.Text;
            //addFileExtension(input.Text);

            extension.Replace(" ", "%20");
            extension.Replace("\\", "/");
            extension.TrimStart('/');

            CheckBox added = new CheckBox()
            {
                IsChecked = true,
                Content = extension,
                Tag = extension
            };

            iedLogFoldersIncludedInput.Items.Remove(item);
            iedLogFoldersIncludedInput.Items.Add(added);

            IsIedSaved = false;

            return added;
        }

        private CheckBox checkBoxInputToExtension(CheckBox item)
        {
            TextBox input = (TextBox)item.Content;
            string extension = input.Text;
            //addFileExtension(input.Text);
            if (!input.Text.StartsWith("."))
            {
                extension = "." + extension;
            }

            extension.Replace(" ", "");
            CheckBox added = new CheckBox()
            {
                IsChecked = true,
                Content = extension,
                Tag = extension
            };

            iedLogExtensionsIncludedInput.Items.Remove(item);
            iedLogExtensionsIncludedInput.Items.Add(added);

            IsIedSaved = false;

            return added;
        }

        private void addFolderButton_Click(object sender, RoutedEventArgs e)
        {
            TextBox input = new TextBox()
            {
                Text = "new folder",

            };

            CheckBox item = new CheckBox()
            {
                Content = input,
                IsChecked = true
            };

            input.KeyDown += (object s, KeyEventArgs ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    checkBoxInputToFolder(item);
                }
            };

            iedLogFoldersIncludedInput.Items.Add(item);
        }

        private void delFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (iedLogFoldersIncludedInput.SelectedItem == null) return;

            iedLogFoldersIncludedInput.Items.Remove(iedLogFoldersIncludedInput.SelectedItem);
            IsIedSaved = false;
        }
    }
}
