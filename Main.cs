using IEDCollector.Lib;
using System.Collections.Generic;
using System.Diagnostics;

namespace IEDCollector
{
    class Main
    {
        public Logs logs;
        public Runner collectionProcess;

        public static void main()
        {
            Debug.WriteLine("MAIN");
            new XMLConfig(@"C:\Users\fillo\Desktop\Projects\Engiprot\IEDCollector\bin\Debug\DummyFile.txt", new Dictionary<string, object>()
            {
                { "test", new Dictionary<string, object>()
                    {
                        {  "child1ofTest", "asd" },
                        { "child2ofTest", "asd2" },
                        { "child3ofTest", new Dictionary<string,object>()
                            {
                                { "asd", "asddd" }
                            } 
                        }
                    } 
                } 
            });

        }
    }
}
