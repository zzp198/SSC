using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SSC.Core;

public class ServerPlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        if (Main.netMode == NetmodeID.SinglePlayer && ModContent.GetInstance<ServerConfig>().UITester)
        {
            ModContent.GetInstance<ServerSystem>().UI?.SetState(new ServerViewer()); // 唯一设置界面的地方
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