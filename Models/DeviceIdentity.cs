namespace EasyControl.Models;

/// <summary>
/// Stable identity for a DirectInput controller so a profile can be recognised
/// again on a later run.
/// </summary>
public sealed class DeviceIdentity
{
    public string ProductName { get; set; } = string.Empty;

    public Guid ProductGuid { get; set; }

    public int Usage { get; set; }

    public int UsagePage { get; set; }

    /// <summary>Key used to match a connected device against a saved profile.</summary>
    public string MatchKey => $"{ProductGuid:N}|{ProductName}|{UsagePage:X4}:{Usage:X4}";

    public override string ToString() => string.IsNullOrWhiteSpace(ProductName) ? "Unknown controller" : ProductName;
}
