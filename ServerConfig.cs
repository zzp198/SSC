﻿using System.ComponentModel;
using Terraria.ModLoader.Config;
using Terraria.Social;

namespace SSC;

// ReSharper disable once FieldCanBeMadeReadOnly.Global
public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Range(1, 500), DefaultValue(100)]
    //-
    public int StartLife = 100;

    [Range(0, 200), DefaultValue(20)]
    //-
    public int StartMana = 20;

    [DefaultValue(false), ReloadRequired]
    //-
    public bool EveryWorld = false;

    [DefaultValue(false)]
    //-
    public bool Tip = false;

    public override void OnLoaded()
    {
        // SocialAPI.Cloud.GetFiles();
    }
}