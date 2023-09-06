using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace IEDCollector.Lib
{


    internal class XMLConfig
    {
        public string File { get; set; }
        public XDocument Content { get; set; }

        public XMLConfig(string filePath, XDocument defaultValue)
        {
            this.File = filePath;
            try { Load(); } catch (Exception e)
            {
                this.Content = defaultValue;
                this.Save();
            }
        }

        private void Save()
        {
            this.Content.Save(this.File);
        }

        private void Load()
        {
            this.Content = XDocument.Load(this.File); // IOException to be handled on above level
            this.Get(new List<string>());
        }

        public string Get (List<string> path)
        {
            //throw new NotImplementedException();

            XElement current = this.Content.Root;
            for (int i = 0; i < path.Count; i++)
            {
                string index = path[i];
                current = current.Element(index);
                if (current == null)
                {
                    throw new XmlException("Invalid path");
                }
            }

            return current.Value;
        }

        public double GetDouble(List<string> path)
        {
            return Double.Parse(Get(path));
        }

        public int GetInt(List<string> path)
        {
            return int.Parse(Get(path));
        }

        public bool GetBool(List<string> path)
        {
            return Boolean.Parse(Get(path));
        }

        public void Set<T>(List<string> path, T value) {
            XElement current = this.Content.Root;
            for (int i = 0; i < path.Count; i++)
            {
                string index = path[i];
                current = current.Element(index);
                if (current == null)
                {
                    throw new XmlException("Invalid path");
                }
            }

            current.Value = value.ToString();
        }
    }
}
