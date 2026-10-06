using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyControl.Models;

namespace EasyControl.ViewModels;

/// <summary>
/// One Xbox 360 output the user can assign a physical input to. Clicking the row
/// starts a live capture; the next matching input on the controller is bound.
/// </summary>
public sealed partial class TargetRowViewModel : ObservableObject
{
    private readonly Action<TargetRowViewModel> _assign;
    private readonly Action<TargetRowViewModel> _clear;
    private readonly Action<TargetRowViewModel> _invert;
    private bool _suppressInvert;

    public TargetRowViewModel(
        TargetDescriptor descriptor,
        Action<TargetRowViewModel> assign,
        Action<TargetRowViewModel> clear,
        Action<TargetRowViewModel> invert)
    {
        Target = descriptor.Key;
        Label = descriptor.Label;
        Kind = descriptor.Kind;
        _assign = assign;
        _clear = clear;
        _invert = invert;
    }

    public string Target { get; }

    public string Label { get; }

    public TargetKind Kind { get; }

    /// <summary>Only the analog stick axes can be inverted.</summary>
    public bool CanInvert => Kind == TargetKind.Stick;

    [ObservableProperty]
    private string _sourceKey = string.Empty;

    [ObservableProperty]
    private string _sourceLabel = "Not set";

    [ObservableProperty]
    private bool _hasSource;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _isInverted;

    public string Status => IsCapturing ? ListeningText : SourceLabel;

    public string Hint => Kind switch
    {
        TargetKind.Stick => "Move stick",
        TargetKind.Trigger => "Pull trigger",
        _ => "Press button"
    };

    public bool ShowHint => IsCapturing || !HasSource;

    private string ListeningText => Kind switch
    {
        TargetKind.Stick => "Move the stick\u2026",
        TargetKind.Trigger => "Pull the trigger\u2026",
        _ => "Press a button\u2026"
    };

    public void Apply(string? source, bool inverted)
    {
        SourceKey = source ?? string.Empty;
        HasSource = !string.IsNullOrEmpty(source);
        SourceLabel = source is null ? "Not set" : SourceCatalog.LabelFor(source);

        if (IsInverted != inverted)
        {
            _suppressInvert = true;
            IsInverted = inverted;
            _suppressInvert = false;
        }
    }

    partial void OnSourceLabelChanged(string value) => OnPropertyChanged(nameof(Status));

    partial void OnIsCapturingChanged(bool value)
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ShowHint));
    }

    partial void OnHasSourceChanged(bool value) => OnPropertyChanged(nameof(ShowHint));

    partial void OnIsInvertedChanged(bool value)
    {
        if (!_suppressInvert)
        {
            _invert(this);
        }
    }

    [RelayCommand]
    private void Assign() => _assign(this);

    [RelayCommand]
    private void Clear() => _clear(this);
}
