using System.Collections.Generic;
using System.ComponentModel;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace SSC.Core;

public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // [Header("")]
    [DefaultValue(60)] //
    [Range(10, int.MaxValue)]
    public int AutoSave = 60;

    [DefaultValue(true)]
    public bool ShowSavedMassage = true;

    [DefaultValue(false)] //
    [ReloadRequired]
    [BackgroundColor(255, 100, 100)]
    public bool Save4World = false;

    [DefaultListValue(1)] //
    [Expand(false)]
    public Dictionary<ItemDefinition, int> StartItems = new();

    [DefaultValue("")] //
    public string Password = "";

    public override bool AcceptClientChanges(ModConfig obj, int whoAmI, ref NetworkText message)
    {
        if (obj is not ServerConfig serverConfig)
        {
            return false;
        }

        var flag = serverConfig.Password == SSC.Password;
        message = flag ? NetworkText.FromLiteral("配置保存成功.") : NetworkText.FromLiteral("密码错误.");

        serverConfig.Password = "";
        return flag;
    }

    public override void OnLoaded()
    {
        Password = "";
    }

    public override void OnChanged()
    {
        Password = "";
    }
}
