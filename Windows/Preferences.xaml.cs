using System.Windows;
using System.Windows.Controls;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Preferences : Window
    {
        public Preferences() { 
            InitializeComponent(); 
            Language selected = Lang.language;

            foreach (Language l in IEDCollector.Language.GetValues(typeof(Language)))
            {
                if (l.Equals(selected))
                {
                    this.languageCombo.Items.Add(new ComboBoxItem() { Content = l, IsSelected = true, Tag = l });
                }
                else
                {
                    this.languageCombo.Items.Add(new ComboBoxItem() { Content = l, Tag = l });
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Language comboSel = (Language)((ComboBoxItem)this.languageCombo.SelectedItem).Tag;
            if (comboSel != Lang.language && MessageBox.Show(Properties.Resources.messagebox_confirm_change_language, "IEDCollector", MessageBoxButton.YesNo, MessageBoxImage.Question).Equals(MessageBoxResult.Yes))
            {
                Lang.language = comboSel;
                Globals.config.save();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }

            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
