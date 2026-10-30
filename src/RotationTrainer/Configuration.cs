using Dalamud.Configuration;
using RotationTrainer.Core;

namespace RotationTrainer;

public enum Accent { Sky, Violet, Mint, Gold }

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public Dictionary<string, JobSelection> Selections { get; set; } = new();
    public bool UsePotions { get; set; } = false;
    public bool StartWithCountdown { get; set; } = true;
    public bool ResetOnLeavingCombat { get; set; } = true;
    public bool ShowTrail { get; set; } = true;
    public TrailStyle Style { get; set; } = TrailStyle.Rail;
    public TrailSize Size { get; set; } = TrailSize.Big;
    public AdvanceMode Advance { get; set; } = AdvanceMode.Manual;
    public int AbilitiesAhead { get; set; } = 8;
    public KeybindMode Keybinds { get; set; } = KeybindMode.Auto;
    public bool FlashWrong { get; set; } = true;
    public bool LockTrail { get; set; } = false;
    public bool UseCustomSpeed { get; set; } = false;
    public int CustomSpellSpeed { get; set; } = 420;
    public int CustomSkillSpeed { get; set; } = 420;
    /// <summary>Animation lock = 0.6 s + LatencyMs / 1000.</summary>
    public int LatencyMs { get; set; } = 50;
    public Accent Accent { get; set; } = Accent.Sky;
    public float GlassOpacity { get; set; } = 0.7f;
    public bool TrailGlassStrip { get; set; } = true;
}
