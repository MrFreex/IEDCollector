using Microsoft.IdentityModel.Tokens;
using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace IEDCollector.Windows
{
    /// <summary>
    /// Interaction logic for LicenseRequest.xaml
    /// </summary>
    public partial class LicenseRequest : Window
    {
        private string generateEmailLink()
        {
            string uri = "mailto:geral@engiprot.pt";

            uri = uri + String.Format("?subject={0}", System.Web.HttpUtility.UrlEncode(String.Format("[{0}]IEDCollector License Request", fullName.Text)));
            uri = uri + String.Format("&body={0}", System.Web.HttpUtility.UrlEncode(String.Format("Hello,\n\nI'm {0} and I would like to request a license for IEDCollector.\n\nFull Name: {0}\nLocation: {1}\nRequest Id: {2}", fullName.Text, location.Text, new ComputerInfo().CpuId)));

            return uri.Replace("+", "%20");
        }

        private void saveEmailFile()
        {
            string cpuId = Base64UrlEncoder.Encode(new ComputerInfo().CpuId);
            XDocument licenseRequest = new XDocument(
                               new XElement("LicenseRequest",
                                                  new XElement("FullName", fullName.Text),
                                                                     new XElement("Location", location.Text),
                                                                                        new XElement("RequestId", cpuId),
                                                                                        new XElement("Date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
                                                                                                       )

                                          );

            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog()
            {
                Description = "Select a folder to save the request file",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                Multiselect = false,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            string finalPath = Path.Combine(dialog.SelectedPath, cpuId + "_License_Request.xml");

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    licenseRequest.Save(finalPath);
                    //File.WriteAllText(Path.Combine(dialog.SelectedPath, "License_Request.txt"), fileLines);
                }
                catch (IOException)
                {
                    MessageBox.Show(Properties.Resources.messagebox_error_saving_file, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error); return;
                }

                MessageBox.Show(String.Format(Properties.Resources.messagebox_license_request_saved, finalPath), Properties.Resources.messagebox_license_request_saved_title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public LicenseRequest()
        {
            InitializeComponent();
            location_TextChanged(null, null);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //Process.Start(generateEmailLink());
            saveEmailFile();
            this.DialogResult = true;
            this.Close();
        }

        private void fullName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            location_TextChanged(sender, e);
        }

        private void location_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (location.Text.Length > 0 && fullName.Text.Length > 0)
            {
                req.IsEnabled = true;
            }
            else
            {
                req.IsEnabled = false;
            }
        }
    }
}
