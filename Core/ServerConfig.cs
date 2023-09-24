﻿using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace SSC.Core;

public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // [Header("")]
    [DefaultValue(60)] //
    [Range(0, int.MaxValue)]
    public int AutoSaveCountdown = 60;

    [DefaultValue(false)] //
    [ReloadRequired]
    [BackgroundColor(255, 100, 100)]
    public bool SaveForWorld = false;

    [DefaultListValue(1)] //
    [Expand(false)]
    public Dictionary<ItemDefinition, int> StartItems = new();

    [DefaultValue("")] //
    public string Password = "";

    public override bool AcceptClientChanges(ModConfig obj, int from, ref string message)
    {
        if (obj is not ServerConfig serverConfig)
        {
            return false;
        }

        var flag = serverConfig.Password == SSC.Password;
        message = flag ? "配置保存成功." : "密码错误.";

        serverConfig.Password = "";
        return flag;
    }

    public override void OnChanged()
    {
        Password = "";
    }
}