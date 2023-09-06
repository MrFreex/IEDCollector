using IEDCollector.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IEDCollector
{
    class Main
    {
        public Logs logs;
        public Runner collectionProcess;

        public static void main()
        {
            new XMLConfig("C:\\Users\\fillo\\Desktop\\Projects\\Engiprot\\IEDCollector\\bin\\Debug\\DummyFile.txt").load();

        }
    }
}
