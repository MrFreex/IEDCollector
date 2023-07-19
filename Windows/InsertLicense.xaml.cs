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

            //this.requestLink.NavigateUri = new Uri(generateEmailLink());
        }

        private void requestLink_Click(object sender, RoutedEventArgs e)
        {
            bool? result = new LicenseRequest().ShowDialog();
            if (result != null && result == true)
            {
                MessageBox.Show("Thank you for requesting a license. You will hear from us as soon as possible.", "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.DialogResult == null)
                this.DialogResult = false;
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            string license = this.licenseBox.Text;
            if (license.Equals(String.Empty))
            {
                MessageBox.Show("Please insert a valid license.", "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Security.validateLicense(license))
            {
                //MessageBox.Show("License successfully validated.", "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("License not valid. Request a license if you have none.", "IEDCollector", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
