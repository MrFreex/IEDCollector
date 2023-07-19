using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace IEDCollector.Windows
{
    /// <summary>
    /// Interaction logic for LicenseRequest.xaml
    /// </summary>
    public partial class LicenseRequest : Window
    {
        private string generateEmailLink()
        {
            string uri = "mailto:filippolissandrin@gmail.com";

            uri = uri + String.Format("?subject={0}", System.Web.HttpUtility.UrlEncode(String.Format("[{0}]IEDCollector License Request", fullName.Text)));
            uri = uri + String.Format("&body={0}", System.Web.HttpUtility.UrlEncode(String.Format("Hello,\n\nI'm {0} and I would like to request a license for IEDCollector.\n\nFull Name: {0}\nLocation: {1}\nRequest Id: {2}", fullName.Text, location.Text, new ComputerInfo().CpuId)));

            return uri.Replace("+", "%20");
        }

        public LicenseRequest()
        {
            this.DialogResult = false;
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(generateEmailLink());
            this.DialogResult = true;
            this.Close();
        }
    }
}
