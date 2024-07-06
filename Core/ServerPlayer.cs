using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SSC.Core;

public class ServerPlayer : ModPlayer
{
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

    public override void OnEnterWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && ModContent.GetInstance<ServerConfig>().LimitClientMods)
        {
            List<string> ClientMods = [];
            ClientMods.AddRange(from mod in ModLoader.Mods where mod.Side == ModSide.Client select mod.Name);

            var mp = Mod.GetPacket();
            mp.Write((byte)MessageID.ClientModCheck);
            mp.Write(ClientMods.Count);
            foreach (var name in ClientMods)
            {
                mp.Write(name);
            }

            MessageManager.FrameSend(mp);
        }
    }
}