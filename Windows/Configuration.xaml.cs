using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Windows;

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
        }

        private void browseDataLocation(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog folderBrowserDialog = new VistaFolderBrowserDialog();
            folderBrowserDialog.InitialDirectory = ConfigFolder.Path;
            folderBrowserDialog.Description = "Select the new configuration folder";
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
                    MessageBox.Show("The folder doesn't exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void save(object sender, RoutedEventArgs e)
        {
            //TODO: Save data

            FSyncConfiguration config = Globals.config.config;

            if (keepAllLogFiles.IsChecked != true)
            {
                try
                {
                    int.Parse(logFilesKept.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show("The number of log files kept must be a number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            config.logFilesKept = (bool)keepAllLogFiles.IsChecked ? -1 : int.Parse(logFilesKept.Text);
            config.startWithWindows = (bool)startWithWindows.IsChecked;

            if (dataLocation.Text != ConfigFolder.Path)
            {
                if (MessageBox.Show("Changing the configuration folder will reset the application. Are you sure you want to continue? The application will restart to apply the configuration.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
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
                    MessageBox.Show("The folder doesn't exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }


            }

            // TODO : profile folder

            Globals.config.save();

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
