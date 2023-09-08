using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IEDCollector.Lib
{
    class FileData
    {
        private string fileName;

        public FileData(string fileName) { this.fileName = fileName; }

        public string getFileName() { return fileName; }
    }
}
