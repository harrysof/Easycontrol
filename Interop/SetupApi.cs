using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EasyControl.Interop;

/// <summary>
/// Minimal SetupAPI wrapper used to find the PnP instance ids of a physical HID
/// device (so it can be hidden from other applications via HidHide).
/// </summary>
internal static class SetupApi
{
    private const uint DigcfPresent = 0x02;
    private static readonly Guid HidClassGuid = new("745a17a0-74d3-11d0-b6fe-00a0c90f57da");

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint CbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevInfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfoData,
        StringBuilder deviceInstanceId,
        uint deviceInstanceIdSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    /// <summary>Returns HID-class instance ids whose id contains <paramref name="hardwareId"/>.</summary>
    public static IReadOnlyList<string> FindHidInstanceIds(string hardwareId)
    {
        var results = new List<string>();
        var classGuid = HidClassGuid;
        var set = SetupDiGetClassDevsW(ref classGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent);

        if (set == IntPtr.Zero || set == new IntPtr(-1))
        {
            return results;
        }

        try
        {
            var size = (uint)Marshal.SizeOf<SpDevInfoData>();
            var data = new SpDevInfoData { CbSize = size };
            uint index = 0;

            while (SetupDiEnumDeviceInfo(set, index, ref data))
            {
                var buffer = new StringBuilder(512);
                if (SetupDiGetDeviceInstanceIdW(set, ref data, buffer, (uint)buffer.Capacity, out _))
                {
                    var id = buffer.ToString();
                    if (id.Contains(hardwareId, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(id);
                    }
                }

                data.CbSize = size;
                index++;
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }

        return results;
    }
}
