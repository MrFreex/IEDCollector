using System.Windows;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for NewProfileDialog.xaml
    /// </summary>
    public partial class NewProfileDialog : Window
    {
        public string profileName => this.ProfileName.Text;

        public NewProfileDialog()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == null)
                DialogResult = false;
        }

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
