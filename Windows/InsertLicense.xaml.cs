using System;
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
            }
            licenseBox.Password = Security.License;
            
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
            string license = this.licenseBox.Password;
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

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(this.licenseBox.Password);
        }

        private void FreeMode_Click(object sender, RoutedEventArgs e)
        {
            Security.setFreeMode(true);
            this.DialogResult = true;
            //this.Close();
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }
}
