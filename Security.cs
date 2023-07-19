using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;
using Standard.Licensing;
using Standard.Licensing.Validation;
using System;
using System.Linq;

namespace IEDCollector
{
    internal enum SecurityValidationResult
    {
        OK, UNSET, INVALID
    }
    internal class Security
    {
        private const string PUBLIC_KEY = @"MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE0qOslzOs2EhWuty6J8L7Okh/hznY5PXgS/YJSqOiGGabx1yAiPn7SzZ3tZnfvRpc+MiqIWLAHT+p0le+IBCzPQ==";

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
            License parsedLicense = License.Load(license);

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

        public static SecurityValidationResult validate()
        {
            using (RegistryKey licenseStorage = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\" + Globals.FOLDERSNAME))
            {
                if (licenseStorage == null) return SecurityValidationResult.UNSET;

                string license = licenseStorage.GetValue("license").ToString();

                return validateLicense(license) ? SecurityValidationResult.OK : SecurityValidationResult.INVALID;
            }
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
    }
}
