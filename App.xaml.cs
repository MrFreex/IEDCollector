using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public void initCulture()
        {
            Language l = Lang.language;
            Debug.WriteLine(l.ToString());
            Debug.WriteLine(Lang.LanguageToCulture[l]);
            if (l != Language.English)
            {
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(Lang.LanguageToCulture[l]);
                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(Lang.LanguageToCulture[l]);
                Debug.WriteLine(Thread.CurrentThread.CurrentUICulture.DisplayName);
            }
        }

        App()
        {
            initCulture();
        }
    }
}
