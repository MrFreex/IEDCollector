using System;
using System.Collections.Generic;
using System.Management;



namespace IEDCollector
{
    internal class HardDrive
    {
        public string Model { get; set; }
        public string InterfaceType { get; set; }
        public string Caption { get; set; }
        public string SerialNo { get; set; }
    }

    internal class ComputerInfo
    {
        public string CpuId
        {
            get
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select ProcessorID from Win32_Processor");
                ManagementObjectCollection mObject = searcher.Get();

                foreach (ManagementObject obj in mObject)
                {
                    return obj["ProcessorID"].ToString();
                }

                return String.Empty;
            }
        }

        public List<string> Drives
        {
            get
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

                List<string> hdCollection = new List<string>();

                foreach (ManagementObject wmi_HD in searcher.Get())
                {
                    hdCollection.Add(wmi_HD.GetPropertyValue("SerialNumber").ToString());
                }

                return hdCollection;
            }
        }

        public string MotherBoard
        {
            get
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");

                string mb = String.Empty;

                foreach (ManagementObject wmi_HD in searcher.Get())
                {
                    mb = wmi_HD.GetPropertyValue("SerialNumber").ToString();
                }

                return mb;
            }
        }
    }
}
