using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasyControl.Models;

namespace EasyControl.Services;

public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    public List<GameProfile> Profiles { get; private set; } = [];

    public event Action? Changed;

    public ProfileService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyControl");
        Directory.CreateDirectory(directory);

        _path = Path.Combine(directory, "profiles.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Profiles = JsonSerializer.Deserialize<List<GameProfile>>(json, JsonOptions) ?? [];
            }
        }
        catch
        {
            Profiles = [];
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Profiles, JsonOptions));
        }
        catch
        {
            // best effort
        }

        Changed?.Invoke();
    }

    public GameProfile? FindForDevice(DeviceIdentity identity) =>
        Profiles.FirstOrDefault(p => p.Device.MatchKey == identity.MatchKey);

    public GameProfile? FindDefault() =>
        Profiles.FirstOrDefault(p => p.IsDefault);

    public GameProfile GetOrCreate(DeviceIdentity identity, string name)
    {
        var existing = FindForDevice(identity);
        if (existing is not null)
        {
            return existing;
        }

        var profile = GameProfile.CreateFor(identity, name);
        Profiles.Add(profile);
        Save();
        return profile;
    }

    public void SetDefault(GameProfile profile)
    {
        foreach (var item in Profiles)
        {
            item.IsDefault = ReferenceEquals(item, profile);
        }

        Save();
    }

    public void Remove(GameProfile profile)
    {
        Profiles.Remove(profile);
        Save();
    }
}
