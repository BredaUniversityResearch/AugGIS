interface ICustomXRUIVisualHelper
{
    public enum CustomState
    {
        Normal,
        Hovered,
        Disabled,
        Pressed,
        Selected,
        Deselected
    }

    public CustomState state { get; }

    public delegate void StateChangeCallback(CustomState newState);
    public StateChangeCallback stateChangeCallback { get; set; }

    public delegate void UpdateProgressBarCallback(float progress);
    public UpdateProgressBarCallback updateProgressBarCallback { get; set; }
}