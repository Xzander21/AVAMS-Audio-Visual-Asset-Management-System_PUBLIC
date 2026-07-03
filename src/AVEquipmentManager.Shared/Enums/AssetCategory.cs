namespace AVEquipmentManager.Shared.Enums;

/// <summary>
/// Classification of audio-visual assets managed by AVAMS.
/// Aligns with the ITIL Service Asset and Configuration Management (SACM)
/// principle of grouping configuration items into well-defined classes.
/// </summary>
public enum AssetCategory
{
    /// <summary>Image projection devices (DLP, LCD, laser projectors).</summary>
    Projector,
    /// <summary>Flat-panel screens, LED walls, monitors used for content delivery.</summary>
    Display,
    /// <summary>Audio capture devices (handheld, lapel, gooseneck, condenser microphones).</summary>
    Microphone,
    /// <summary>Audio output devices (speakers, soundbars, monitor speakers).</summary>
    Speaker,
    /// <summary>Audio amplification units (power amps, integrated amplifiers).</summary>
    Amplifier,
    /// <summary>Audio mixing consoles and signal routers.</summary>
    AudioMixer,
    /// <summary>Video capture devices (webcams, PTZ cameras, document cameras).</summary>
    Camera,
    /// <summary>Interactive learning surfaces (smart boards, touchscreen displays).</summary>
    InteractiveBoard,
    /// <summary>Signal switchers, distribution amplifiers, HDMI matrices, and cabling hubs.</summary>
    SignalDistribution,
    /// <summary>AV control surfaces and rack controllers.</summary>
    ControlSystem,
    /// <summary>Items that do not fit the above categories.</summary>
    Other
}
