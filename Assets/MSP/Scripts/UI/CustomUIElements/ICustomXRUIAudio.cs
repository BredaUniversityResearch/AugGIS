interface ICustomXRUIAudio
{
    public enum SoundType
    {
        BeginPress,
        EndPress
    }
    public delegate void PlaySound(SoundType type);
    public PlaySound playSound { get; set; }
}