using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader.Config;

namespace SSC.Core;

[BackgroundColor(164, 153, 190)]
public class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // [Header("")]
    [DefaultValue(true)] //
    [ReloadRequired]
    public bool PlayersAnyWorld = true;

    [DefaultValue(false)] //
    [ReloadRequired]
    public bool PlayersPreWorld = false;

    [Range(1, 500), Increment(1)] //
    [DefaultValue(100)]
    public int StartingLife = 100;

    [Range(0, 200), Increment(1)] //
    [DefaultValue(20)]
    public int StartingMana = 20;

    public List<string> Administrators = new();

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref string message)
    {
        // 确认管理员权限前先清除掉已不存在的管理员
        CheckoutAdministrators();

        if (Administrators.Count == 0)
        {
            return true;
        }

        if (Administrators.Contains(Main.player[whoAmI].name))
        {
            return true;
        }

        message = "You are not an administrator!";
        return false;
    }

    public void CheckoutAdministrators()
    {
        var names = new DirectoryInfo(SSC.PATH).GetFiles().Select(i => i.Name).ToList();

        for (var i = Administrators.Count - 1; i >= 0; i++)
        {
            if (!names.Contains(Administrators[i]))
            {
                Console.WriteLine($"SSC: 移除已失效的管理员用户 {Administrators[i]} !");
                Administrators.RemoveAt(i);
            }
        }
    }
}