using Ookii.Dialogs.Wpf;
using System;
using System.Windows;

namespace FSync
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
            folderBrowserDialog.RootFolder = Environment.SpecialFolder.LocalApplicationData;
            folderBrowserDialog.Description = "Select the new configuration folder";
            folderBrowserDialog.ShowDialog();

            //folderBrowserDialog.SelectedPath;
        }

        private void save(object sender, RoutedEventArgs e)
        {
            //TODO: Save data
            this.Close();
        }

        private void cancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
