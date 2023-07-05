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
        public readonly string filePath;
        public readonly List<TextBox> outputs;
        public Logs(string folderPath, List<TextBox> outputs)
        {
            this.filePath = Path.Combine(folderPath, string.Format("{0}-{1}-{2} {3}-{4}-{5}.log", DateTime.Now.Day, DateTime.Now.Month, DateTime.Now.Year, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second));
            Directory.CreateDirectory(folderPath);

            foreach (TextBox output in outputs)
            {
                output.IsReadOnly = true;
            }

            this.outputs = outputs;
        }

       

        public void log(string message)
        {
            string finalMsg = string.Format("[{0}] {1}", DateTime.Now.ToString(), message);

            using (StreamWriter logStream = new StreamWriter(this.filePath))
            {
                logStream.WriteLine(finalMsg);

                foreach (TextBox output in outputs)
                {
                    output.AppendText(finalMsg + "\n");
                }
            }
          
        }
    }
}
