using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace IEDCollector
{
    internal class OptimizedBitmapImage : IDisposable
    {
        private FileStream mediaStream;
        private BitmapImage bitmap;

        public BitmapImage Bitmap
        {
            get
            {
                return bitmap;
            }
        }

        public OptimizedBitmapImage(string path)
        {

            bitmap = new BitmapImage();
            mediaStream = new FileStream(path, FileMode.Open);

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.None;
            bitmap.StreamSource = mediaStream;
            bitmap.EndInit();

            bitmap.Freeze();
        }

        public void Dispose()
        {
            if (mediaStream != null)
            {
                mediaStream.Close();
                mediaStream.Dispose();
                mediaStream = null;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            }
        }
    }
}
