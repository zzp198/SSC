using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace SSC.Core;

public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // [Header("")]
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

        message = serverConfig.Password == SSC.Password ? "配置保存成功." : "密码错误.";
        return serverConfig.Password == SSC.Password;
    }

    public override void OnChanged()
    {
        Password = "";
    }
}