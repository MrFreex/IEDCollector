using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IEDCollector
{
    static class Globals
    {
        public const string FOLDERSNAME = "IEDCollector";
        public static OnProfileChange profileChangeHandler = null;
        public static Runner currentProcess = null;
        public static UserConfig config = null;
        private static bool? freeMode = null;

        public static bool IsFreeMode
        {
            get { return freeMode ?? true; }
            set
            {
                if (freeMode == null)
                {
                    freeMode = value;

                }
            }
        }


        public delegate void OnProfileChange(bool renameOnly);

        public static Profile currentProfile
        {
            get { return currProfile; }
            set
            {
                currProfile = value;
                if (profileChangeHandler != null)
                {
                    profileChangeHandler(false);
                }
            }
        }

        private static Profile currProfile;

        public static Logs logs;
        public static GlobalConfiguration globalConfiguration;
    }
}
