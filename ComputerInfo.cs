using System;
using System.Management;



namespace IEDCollector
{
    // Simple class to allow the extraction of the CPU ID
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
    }
}
