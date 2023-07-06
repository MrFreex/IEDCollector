using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FSync
{
    internal class Logs
    {
        private string filePath = null;
        public readonly List<TextBox> outputs;

        private string logContent = "";
        public string Log { get { return this.logContent; } }

        public Logs(List<TextBox> outputs)
        {
            //
            //Directory.CreateDirectory(folderPath);

            foreach (TextBox output in outputs)
            {
                output.IsReadOnly = true;
            }

            this.outputs = outputs;
        }

       

        public void log(string message)
        {
            string finalMsg = string.Format("[{0}] {1}", DateTime.Now.ToString(), message);
            this.logContent += finalMsg + "\n";

            if (this.filePath == null)
            {
                return;
            }

            using (StreamWriter logStream = new StreamWriter(this.filePath))
            {
                logStream.WriteLine(this.logContent);

                foreach (TextBox output in outputs)
                {
                    output.AppendText(finalMsg + "\n");
                }
            }
          
        }

        public void setFolder(string folderPath)
        {
            if (this.filePath != null) throw new InvalidOperationException("filePath already defined");

            this.filePath = Path.Combine(folderPath, string.Format("{0}-{1}-{2} {3}-{4}-{5}.log", DateTime.Now.Day, DateTime.Now.Month, DateTime.Now.Year, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second));
            Directory.CreateDirectory(folderPath);

            this.log(String.Format("Log file set: {0}", filePath));
        }
    }
}
