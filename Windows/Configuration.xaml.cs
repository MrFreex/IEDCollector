using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using IEDCollector.Services.Configuration;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for Configuration.xaml
    /// </summary>
    public partial class Configuration : Window
    {
        public Configuration()
        {

            InitializeComponent();

            // Fill combobox

            logLevels.Items.Add(new ComboBoxItem()
            {
                Content = Properties.Resources.loglevel_basic,
                Tag = LogLevel.Basic,
                IsSelected = Globals.config.config.logLevel == LogLevel.Basic
            });
            logLevels.Items.Add(new ComboBoxItem()
            {
                Content = Properties.Resources.loglevel_detailed,
                Tag = LogLevel.Detailed,
                IsSelected = Globals.config.config.logLevel == LogLevel.Detailed
            });
            logLevels.Items.Add(new ComboBoxItem()
            {
                Content = Properties.Resources.loglevel_debug,
                Tag = LogLevel.Debug,
                IsSelected = Globals.config.config.logLevel == LogLevel.Debug
            });
        }

        private void browseDataLocation(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog folderBrowserDialog = new VistaFolderBrowserDialog();
            folderBrowserDialog.InitialDirectory = ConfigFolder.Path;
            folderBrowserDialog.Description = Properties.Resources.select_new_config_folder;
            bool? result = folderBrowserDialog.ShowDialog();

            if (result != null && (bool)result)
            {
                try
                {
                    Directory.CreateDirectory(folderBrowserDialog.SelectedPath);
                    dataLocation.Text = folderBrowserDialog.SelectedPath;
                }
                catch
                {
                    MessageBox.Show(Properties.Resources.messagebox_folder_doesnt_exist, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void save(object sender, RoutedEventArgs e)
        {

            FSyncConfiguration config = Globals.config.config;

            if (keepAllLogFiles.IsChecked != true)
            {
                try
                {
                    int.Parse(logFilesKept.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show(Properties.Resources.messagebox_number_of_log_files_error, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            config.logFilesKept = (bool)keepAllLogFiles.IsChecked ? -1 : int.Parse(logFilesKept.Text);
            config.startWithWindows = (bool)startWithWindows.IsChecked;
            config.minimizeToTray = (bool)minimizeToTray.IsChecked;


            config.logLevel = (LogLevel)((ComboBoxItem)logLevels.SelectedItem).Tag;

            if (dataLocation.Text != ConfigFolder.Path)
            {
                if (MessageBox.Show(Properties.Resources.messagebox_confirm_folder_change, Properties.Resources.warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(dataLocation.Text);
                    Globals.globalConfiguration.setConfigFolder(dataLocation.Text);
                    Globals.globalConfiguration.save();

                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
                catch
                {
                    MessageBox.Show(Properties.Resources.messagebox_folder_doesnt_exist, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }


            }

            // TODO : profile folder

            Globals.config.save();
            Globals.logs.updateLogLevelSelectors();
            this.Close();
        }

        private void cancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void keepAllFiles_Checked(object sender, RoutedEventArgs e)
        {
            if (keepAllLogFiles.IsChecked == true)
            {
                logFilesKept.IsEnabled = false;
                logFilesKept.Text = "";
            }
            else
            {
                logFilesKept.IsEnabled = true;
                logFilesKept.Text = "20";
            }
        }
    }
}
