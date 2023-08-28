using Microsoft.IdentityModel.Tokens;
using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Windows;

namespace IEDCollector.Windows
{
    /// <summary>
    /// Interaction logic for InsertLicense.xaml
    /// </summary>
    public partial class InsertLicense : Window
    {

        public InsertLicense()
        {
            InitializeComponent();
            if (!Security.HasLicense)
            {
                removeLicenseButton.IsEnabled = false;
                export_license.IsEnabled = false;
                license_ok_nok.Content = Properties.Resources.current_license_nok;
            }
            else
            {
                license_ok_nok.Content = Properties.Resources.current_license_ok;
            }
            //licenseBox.Text = Security.License;

            //this.requestLink.NavigateUri = new Uri(generateEmailLink());
        }

        private void requestLink_Click(object sender, RoutedEventArgs e)
        {
            bool? result = new LicenseRequest().ShowDialog();
            if (result != null && result == true)
            {
                MessageBox.Show(Properties.Resources.messagebox_thanks_for_requesting_license, "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            string license;

            try
            {
                license = File.ReadAllText(this.licenseBox.Text);
            }
            catch (IOException)
            {
                MessageBox.Show(Properties.Resources.messagebox_licensefile_not_found, Properties.Resources.messagebox_licensefile_not_found_title, MessageBoxButton.OK, MessageBoxImage.Error); return;
            }



            if (license.Equals(String.Empty))
            {
                MessageBox.Show(Properties.Resources.messagebox_insert_valid_license, "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Security.validateLicense(license))
            {
                //MessageBox.Show("License successfully validated.", "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Information);
                Security.setLicense(license);
                Security.setFreeMode(false);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show(Properties.Resources.messagebox_license_invalid, "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult reallyRemove = MessageBox.Show(Properties.Resources.messagebox_remove_license_confirmation, Properties.Resources.confirm, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (reallyRemove.Equals(MessageBoxResult.Yes))
            {
                Security.removeLicense();
                // Restart to prevent illegal usage
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            //Clipboard.SetText(this.licenseBox.Text);
            VistaOpenFileDialog dialog = new VistaOpenFileDialog()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Title = "Select the license file",
                Filter = "License files (*.txt)|*.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                this.licenseBox.Text = dialog.FileName;
            }
        }

        private void FreeMode_Click(object sender, RoutedEventArgs e)
        {
            Security.setFreeMode(true);
            this.DialogResult = true;
            //this.Close();
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void export_license_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog()
            {
                Description = "Select the folder where you want to save the license file",
                UseDescriptionForTitle = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == true)
            {
                string cpuId = Base64UrlEncoder.Encode(new ComputerInfo().CpuId);
                File.WriteAllText(Path.Combine(dialog.SelectedPath, cpuId + "_License_Key.txt"), Security.License);
            }
        }
    }
}
