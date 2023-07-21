using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;
using Standard.Licensing;
using Standard.Licensing.Validation;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using License = Standard.Licensing.License;

namespace IEDCollector
{
    internal enum SecurityValidationResult
    {
        OK, UNSET, INVALID
    }
    internal class Security
    {
        private const string PUBLIC_KEY = @"MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE0qOslzOs2EhWuty6J8L7Okh/hznY5PXgS/YJSqOiGGabx1yAiPn7SzZ3tZnfvRpc+MiqIWLAHT+p0le+IBCzPQ==";

        public static bool HasLicense => validate().Equals(SecurityValidationResult.OK);
        public static string License => getLicense() ?? String.Empty;

        private static string decodeLicense(string license)
        {
            try
            {
                return Base64UrlEncoder.Decode(license);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static bool validateLicense(string license)
        {
            license = decodeLicense(license);
            ComputerInfo info = new ComputerInfo();
            if (license == null) return false;
            License parsedLicense;
            try
            {
                parsedLicense = Standard.Licensing.License.Load(license);
            } catch (Exception e)
            {
                return false;
            }
            

            return !parsedLicense.Validate().Signature(PUBLIC_KEY)
            .And()
            .AssertThat(lic => // Check Device Identifier matches.
                       lic.AdditionalAttributes.Get("DeviceIdentifier") == info.CpuId,
                       new GeneralValidationFailure()
                       {
                           Message = "Invalid Device.",
                           HowToResolve = "Contact the supplier to obtain a new license key."
                       })
                   .AssertValidLicense().Any();
        }

        private static string getLicense()
        {
            using (RegistryKey licenseStorage = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
            {
                if (licenseStorage == null) return null;

                object value = licenseStorage.GetValue("license");

                if (value == null) return null;

                string license = value.ToString();

                return license;
            }
        }

        public static SecurityValidationResult validate()
        {
            string license = getLicense();
            if (license == null) return SecurityValidationResult.UNSET;
            return validateLicense(license) ? SecurityValidationResult.OK : SecurityValidationResult.INVALID;
        }

        public static SecurityValidationResult setLicense(string key)
        {
            try
            {
                using (RegistryKey licenseStorage = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
                {
                    licenseStorage.SetValue("license", key);
                    return SecurityValidationResult.OK;
                }
            }
            catch (Exception e)
            {
                return SecurityValidationResult.INVALID;
            }

        }

        public static void removeLicense()
        {
            try
            {
                using (RegistryKey licenseStorage = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
                {
                    licenseStorage.DeleteValue("license");
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool isFreeMode()
        {
            using (RegistryKey licenseStorage = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
            {
                if (licenseStorage == null) return false;

                object value = licenseStorage.GetValue("freemode");

                if (value == null) return false;

               

                return value.Equals("1") ? true : false;
            }
        }

        public static bool IsFreeMode => isFreeMode();

        public static void setFreeMode(bool set)
        {
            try
            {
                using (RegistryKey licenseStorage = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
                {
                    licenseStorage.SetValue("freemode", set ? "1" : "0");
                }
            }
            catch (Exception e)
            {
                
            }
        }
    }
}
