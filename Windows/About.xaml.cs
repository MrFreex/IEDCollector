using System.Windows;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public string VersionString
        {
            get
            {
                return Properties.Resources.ied_collector_version + " " + AppInfo.Version;
            }
        }

        public About()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        private void close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
