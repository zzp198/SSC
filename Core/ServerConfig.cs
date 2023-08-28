using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace SSC.Core;

[BackgroundColor(164, 153, 190)]
public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // [Header("")]
    [DefaultValue(false)] //
    [ReloadRequired]
    public bool SaveForWorld = false;

    // [Range(1, 500), Increment(1)] //
    // [DefaultValue(100)]
    // [ReloadRequired]
    // public int StartingLife = 100;
    //
    // [Range(0, 200), Increment(1)] //
    // [DefaultValue(20)]
    // [ReloadRequired]
    // public int StartingMana = 20;
}