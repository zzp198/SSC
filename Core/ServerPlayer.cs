using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace SSC.Core;

public class ServerPlayer : ModPlayer
{
    static uint Countdown;

    public override void PostUpdate()
    {
        Countdown++;
        if (Countdown > 3600)
        {
            Countdown = 0;
            if (Main.ActivePlayerFileData.Path.EndsWith(".SSC"))
            {
                Player.SavePlayer(Main.ActivePlayerFileData);
            }
        }
    }

    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        var items = new List<Item>();
        foreach (var (item, stack) in ModContent.GetInstance<ServerConfig>().StartItems)
        {
            if (item.IsUnloaded)
            {
                continue;
            }

            items.Add(new Item(item.Type, stack, -1));
        }

        return items;
    }
}