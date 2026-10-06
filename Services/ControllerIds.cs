using System;

namespace EasyControl.Services;

/// <summary>VID/PID helpers for DirectInput product GUIDs.</summary>
public static class ControllerIds
{
    /// <summary>
    /// DirectInput packs the vendor/product into Data1 as (PID &lt;&lt; 16 | VID).
    /// </summary>
    public static (int VendorId, int ProductId) VidPid(Guid productGuid)
    {
        var text = productGuid.ToString("N");
        if (text.Length < 8)
        {
            return (0, 0);
        }

        var data1 = Convert.ToUInt32(text[..8], 16);
        return ((int)(data1 & 0xFFFF), (int)((data1 >> 16) & 0xFFFF));
    }

    /// <summary>True for the virtual controllers EasyControl itself creates via ViGEm.</summary>
    public static bool IsVirtualOutput(Guid productGuid)
    {
        var (vid, pid) = VidPid(productGuid);

        // Xbox 360 virtual pad
        if (vid == 0x045E && pid == 0x028E)
        {
            return true;
        }

        // DualShock 4 virtual pad (v1/v2)
        return vid == 0x054C && (pid == 0x05C4 || pid == 0x09CC);
    }
}
