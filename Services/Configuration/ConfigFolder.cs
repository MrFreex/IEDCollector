using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IEDCollector.Services.Configuration
{
    static class ConfigFolder
    {

        private static string folderPath;

        public static string Path { get { return folderPath; } set { setFolderPath(value); } }

        public const string LOGS = "logs";
        public const string LOCALES = "locales";
        public const string PROFILES = "profiles";
        public const string IEDLOGSROOT = "IEDlogs";

        public const string LASTPROFILEFILE = ".lastprofile";

        public static string extend(string constant)
        {
            return System.IO.Path.Combine(folderPath, constant);
        }

        private static void setFolderPath(string folderPath)
        {
            ConfigFolder.folderPath = folderPath;
        }
    }
}
