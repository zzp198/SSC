using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.Social;

namespace SSC;

// ReSharper disable once FieldCanBeMadeReadOnly.Global
public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Range(1, 500)]
    //-
    public int StartLife = 100;

    [Range(0, 200)]
    //-
    public int StartMana = 20;

    [ReloadRequired]
    //-
    public bool EveryWorld = false;

    public override void OnLoaded()
    {
        SocialAPI.Cloud.GetFiles();
    }
}