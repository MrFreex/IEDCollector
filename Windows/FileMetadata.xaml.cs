using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using IEDCollector.Properties;

namespace IEDCollector
{
    /// <summary>
    /// Interaction logic for FileMetadata.xaml
    /// </summary>
    /// 

    public class MetadataWindowFileData
    {
        public ObservableCollection<ListViewItem> ListItems {
            get
            {
                ObservableCollection<ListViewItem> items = new ObservableCollection<ListViewItem>();
                foreach (KeyValuePair<string, string> pair in this.dict)
                {
                    items.Add(new ListViewItem() { Content = pair.Key + ": " + pair.Value, Foreground = Brushes.Black, Background = this.otherItems[pair.Key] == pair.Value ? Brushes.Transparent : Brushes.Gold });
                }

                return items;
            }
        }

        public Dictionary<string, string> dict;
        public Dictionary<string, string> otherItems;
        public string path { get; set; }
        public string FileName => Path.GetFileName(path);

        public MetadataWindowFileData(Dictionary<string,string> dict, string path)
        {
            this.path = path;
            this.dict = dict;
        }
    }

    public partial class FileMetadata : Window
    {

        public MetadataWindowFileData localFileData { get; set; }
        public MetadataWindowFileData remoteFileData { get; set; }

        public FileMetadata(MetadataWindowFileData localFileData, MetadataWindowFileData remoteFileData)
        {
            localFileData.otherItems = remoteFileData.dict;
            this.localFileData = localFileData;
            
            remoteFileData.otherItems = localFileData.dict;
            this.remoteFileData = remoteFileData;
            this.DataContext = this;
            InitializeComponent();
        }
    }
}
