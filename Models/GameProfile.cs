using System.Collections.Generic;
using System.Linq;

namespace EasyControl.Models;

/// <summary>A named mapping profile bound to a specific controller.</summary>
public sealed class GameProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "My Controller";

    public bool IsDefault { get; set; }

    public DeviceIdentity Device { get; set; } = new();

    /// <summary>Source key bound to the screenshot action, or null.</summary>
    public string? ScreenshotSource { get; set; }

    /// <summary>Source key bound to the Game Bar (Win+G) action, or null.</summary>
    public string? GameBarSource { get; set; }

    public double Deadzone { get; set; } = 0.15;

    public List<InputBinding> Bindings { get; set; } = [];

    public string? TargetFor(string source) =>
        Bindings.FirstOrDefault(b => b.Source == source)?.Target;

    /// <summary>The physical source currently bound to <paramref name="target"/>, or null.</summary>
    public string? SourceFor(string target) =>
        Bindings.FirstOrDefault(b => b.Target == target)?.Source;

    /// <summary>
    /// Binds <paramref name="source"/> to <paramref name="target"/>, enforcing a
    /// one-to-one relationship (a source or target already in use is released).
    /// </summary>
    public void Assign(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.Equals(target, Targets.None, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Bindings.RemoveAll(b => b.Source == source || b.Target == target);
        Bindings.Add(new InputBinding(source, target));
    }

    /// <summary>Removes whatever is bound to <paramref name="target"/>.</summary>
    public void ClearTarget(string target)
    {
        Bindings.RemoveAll(b => b.Target == target);
    }

    /// <summary>Whether the binding for <paramref name="target"/> is inverted.</summary>
    public bool InvertFor(string target) =>
        Bindings.FirstOrDefault(b => b.Target == target)?.Invert ?? false;

    /// <summary>Sets the invert flag of the binding for <paramref name="target"/>.</summary>
    public void SetInvert(string target, bool invert)
    {
        var binding = Bindings.FirstOrDefault(b => b.Target == target);
        if (binding is not null)
        {
            binding.Invert = invert;
        }
    }

    public void Set(string source, string target)
    {
        var existing = Bindings.FirstOrDefault(b => b.Source == source);
        if (string.Equals(target, Targets.None, StringComparison.OrdinalIgnoreCase))
        {
            if (existing is not null)
            {
                Bindings.Remove(existing);
            }
            return;
        }

        if (existing is null)
        {
            Bindings.Add(new InputBinding(source, target));
        }
        else
        {
            existing.Target = target;
        }
    }

    public static GameProfile CreateFor(DeviceIdentity device, string name)
    {
        var profile = new GameProfile
        {
            Name = name,
            Device = device
        };

        // Sensible starting point; users can remap anything.
        profile.Set("B1", Targets.A);
        profile.Set("B2", Targets.B);
        profile.Set("B3", Targets.X);
        profile.Set("B4", Targets.Y);
        profile.Set("B5", Targets.LeftShoulder);
        profile.Set("B6", Targets.RightShoulder);
        profile.Set("B7", Targets.View);
        profile.Set("B8", Targets.Menu);
        profile.Set("B9", Targets.LeftThumb);
        profile.Set("B10", Targets.RightThumb);

        profile.Set(SourceCatalog.AxisX, Targets.LeftThumbX);
        profile.Set(SourceCatalog.AxisY, Targets.LeftThumbY);
        profile.Set(SourceCatalog.AxisRx, Targets.RightThumbX);
        profile.Set(SourceCatalog.AxisRy, Targets.RightThumbY);
        profile.Set(SourceCatalog.Slider0, Targets.LeftTriggerAxis);
        profile.Set(SourceCatalog.Slider1, Targets.RightTriggerAxis);

        profile.Set(SourceCatalog.Pov0Up, Targets.DpadUp);
        profile.Set(SourceCatalog.Pov0Down, Targets.DpadDown);
        profile.Set(SourceCatalog.Pov0Left, Targets.DpadLeft);
        profile.Set(SourceCatalog.Pov0Right, Targets.DpadRight);

        return profile;
    }
}
