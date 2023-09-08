using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IEDCollector.Services.Localization
{
    internal class Lang
    {
        public static Language language
        {
            get
            {
                using (RegistryKey licenseStorage = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
                {
                    if (licenseStorage == null) return Language.English;

                    object value = licenseStorage.GetValue(Lang.LANGUAGE);

                    if (value == null) return Language.English;

                    Language license = (Language)int.Parse(value.ToString());

                    return license;
                }
            }

            set
            {
                using (RegistryKey licenseStorage = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
                {
                    licenseStorage.SetValue(Lang.LANGUAGE, ((int)value).ToString());
                }
            }
        }

        public static readonly Dictionary<Language, string> LanguageToCulture = new Dictionary<Language, string>()
        {
            { Language.English, "en-UK" },
            { Language.Portuguese, "pt-PT" },
            { Language.Italian, "it-IT" },
            { Language.Spanish, "es-ES" },
            { Language.French, "fr-FR" },
            //{ Language.German, "de-de" },
        };

        public const string LANGUAGE = "language";
    }
}
