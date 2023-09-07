using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace IEDCollector.Lib
{


    internal class XMLConfig
    {
        public string File { get; set; }
        public XDocument Content { get; set; }

        public XMLConfig(string filePath, Dictionary<string, object> defaultValue, string rootName)
        {
            this.File = filePath;
            try { Load(); }
            catch (Exception e)
            {
                this.Content = new XDocument(this.ParseFromDict(defaultValue, rootName));
                this.Save();
            }
        }

        public XMLConfig(string filePath, Dictionary<string, object> defaultValue) : this(filePath, defaultValue, "root")
        {

        }

        private XElement ParseFromDict(Dictionary<string, object> dictionary, string elName)
        {
            //XElement finalEl = new XElement(elName);
            XElement ret = new XElement(elName);

            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                XElement val;
                if (pair.Value is Dictionary<string, object>)
                {
                    val = ParseFromDict((Dictionary<string, object>)pair.Value, pair.Key);
                }
                else
                {
                    val = new XElement(pair.Key, pair.Value);
                }

                ret.Add(val);
            }

            return ret;
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

        public string Get(List<string> path)
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

        public void Set<T>(List<string> path, T value)
        {
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
