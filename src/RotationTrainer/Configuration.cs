using Dalamud.Configuration;
using RotationTrainer.Core;

namespace RotationTrainer;

public enum Accent { Sky, Violet, Mint, Gold, Sakura }

/// <summary>The glass itself: dark navy, dark grey, or light cherry-blossom pink with dark text.</summary>
public enum Theme { Midnight, Graphite, Sakura }

/// <summary>Which potion counts for a potion step.</summary>
public enum PotionMatch
{
    /// <summary>Any main-stat tincture or gemdraught, so practice needn't burn the newest grade.</summary>
    AnyPotion,
    /// <summary>Only the potion the rotation names (HQ or NQ).</summary>
    Exact,
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public Dictionary<string, JobSelection> Selections { get; set; } = new();
    public bool UsePotions { get; set; } = false;
    public PotionMatch PotionMatch { get; set; } = PotionMatch.AnyPotion;
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
    public Theme Theme { get; set; } = Theme.Midnight;
    public Accent Accent { get; set; } = Accent.Sky;
    /// <summary>Opacity of the settings and builder windows (1 = opaque).</summary>
    public float GlassOpacity { get; set; } = 0.7f;
    /// <summary>Opacity of the trail's glass strip (1 = opaque).</summary>
    public float TrailOpacity { get; set; } = 0.7f;
    public bool TrailGlassStrip { get; set; } = true;
    /// <summary>IDs of built-in rotations already added to the library, so deleted ones are not added back.</summary>
    public HashSet<string> BuiltInsAdded { get; set; } = new();
    /// <summary>Per job: hotbar slots seen showing a replacement action (see <see cref="LearnedKeybind"/>).</summary>
    public Dictionary<string, List<LearnedKeybind>> LearnedKeybinds { get; set; } = new();
}
