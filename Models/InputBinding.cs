namespace EasyControl.Models;

/// <summary>A single physical-input to target mapping.</summary>
public sealed class InputBinding
{
    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = Targets.None;

    public bool Invert { get; set; }

    public InputBinding()
    {
    }

    public InputBinding(string source, string target)
    {
        Source = source;
        Target = target;
    }
}
