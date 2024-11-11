using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace PathsSynchronizer.Core.Support.IO
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static class WindowsExternalHDDHelper
    {
        private static readonly DriveInfo[] _externalDrives = GetExternalDrives();

        internal static bool IsRemovableDrive(string path)
        {
            DriveInfo currentDrive = new(path);
            return
                _externalDrives
                    .Select(x => x.Name)
                    .Contains(currentDrive.Name);
        }

        private static DriveInfo[] GetExternalDrives()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            List<DriveInfo> externalDrives = [];

            ManagementObjectCollection allPhysicalDisks = new ManagementObjectSearcher("select MediaType, DeviceID from Win32_DiskDrive").Get();

            foreach (ManagementBaseObject? physicalDisk in allPhysicalDisks)
            {
                ManagementObjectCollection allPartitionsOnPhysicalDisk = new ManagementObjectSearcher($"associators of {{Win32_DiskDrive.DeviceID='{physicalDisk["DeviceID"]}'}} where AssocClass = Win32_DiskDriveToDiskPartition").Get();
                foreach (ManagementBaseObject? partition in allPartitionsOnPhysicalDisk)
                {
                    if (partition is null)
                    {
                        continue;
                    }

                    ManagementObjectCollection allLogicalDisksOnPartition = new ManagementObjectSearcher($"associators of {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} where AssocClass = Win32_LogicalDiskToPartition").Get();
                    foreach (ManagementBaseObject? logicalDisk in allLogicalDisksOnPartition)
                    {
                        if (logicalDisk is null)
                        {
                            continue;
                        }

                        DriveInfo? drive = drives.FirstOrDefault(x => x.Name.StartsWith(logicalDisk["Name"] as string ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                        if (drive is null)
                        {
                            continue;
                        }

                        string mediaType = physicalDisk["MediaType"] as string ?? string.Empty;
                        if (mediaType.Contains("external", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("removable", StringComparison.OrdinalIgnoreCase))
                        {
                            externalDrives.Add(drive);
                        }
                    }
                }
            }

            return externalDrives.ToArray();
        }
    }
}
