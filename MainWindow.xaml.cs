using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Diagnostics;
using IEC61850.Client;
using IEC61850.Common;
using System.Runtime.InteropServices;
using System.Threading;

namespace FSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    static class Globals
    {
        public static Logs logs;
    }

    class FileData
    {
        private string fileName;

        public FileData(string fileName) { this.fileName = fileName; }

        public string getFileName() { return fileName; }
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            
            InitializeComponent();

            Globals.logs = new Logs("Y:\\Filippo-LogsTest", new List<TextBox>()
            {
                this.Logs0,
                this.Logs1
            });
            Globals.logs.log("Software started");
        }

        public static bool receiveF(object param, byte[] data)
        {
            if (param is FileData)
            {
                FileData fd = (FileData)param;
                Debug.WriteLine(fd.getFileName());
            }
            return true;
        }

        private void TestConnection()

        {   
            var connection = new IedConnection();

            connection.Connect("10.1.21.201", 102);

            List<string> devices = connection.GetServerDirectory();

            foreach (string device in devices)
            {
                Debug.WriteLine(device);
            }

            /*

            FilesDownloader f = new FilesDownloader("/", (List<string> files) => {
                Debug.WriteLine("Thread Over");
                connection.Abort();
            }, connection);
            */
            
        }
    }
}
