using System.Windows;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
        }

        private void close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
