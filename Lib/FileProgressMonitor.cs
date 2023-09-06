namespace IEDCollector
{
    /// <summary>
    /// Handles the file download progress.
    /// </summary>
    internal class FileProgressMonitor
    {
        public delegate void OnFileProgressChanged(double newProgress);
        private OnFileProgressChanged cb;

        private double progress;

        public double Progress
        {
            set { this.cb(value); progress = value; indeterminate = false; }
            get => progress;
        }

        private bool indeterminate = false;

        public bool IsIndeterminate
        {
            set { this.cb(value ? -1 : progress); indeterminate = value; }
            get => indeterminate;
        }

        /// <summary>
        /// Load Assembly, DLL from Embedded Resources into memory.
        /// </summary>
        /// <param name="cb">The callback to execute when the progress changes</param>
        public FileProgressMonitor(OnFileProgressChanged cb)
        {
            this.cb = cb;
        }
    }
}
