namespace EasyControl.Models;

/// <summary>Result of applying a profile to a controller state for one frame.</summary>
public sealed class PadFrame
{
    public bool A;
    public bool B;
    public bool X;
    public bool Y;
    public bool LeftShoulder;
    public bool RightShoulder;
    public bool View;
    public bool Menu;
    public bool LeftThumb;
    public bool RightThumb;
    public bool DpadUp;
    public bool DpadDown;
    public bool DpadLeft;
    public bool DpadRight;
    public bool Guide;

    public float LeftStickX;
    public float LeftStickY;
    public float RightStickX;
    public float RightStickY;

    public float LeftTrigger;
    public float RightTrigger;

    public bool Screenshot;

    public bool GameBar;

    public void Reset()
    {
        A = B = X = Y = false;
        LeftShoulder = RightShoulder = false;
        View = Menu = false;
        LeftThumb = RightThumb = false;
        DpadUp = DpadDown = DpadLeft = DpadRight = false;
        Guide = false;
        LeftStickX = LeftStickY = RightStickX = RightStickY = 0f;
        LeftTrigger = RightTrigger = 0f;
        Screenshot = false;
        GameBar = false;
    }
}
