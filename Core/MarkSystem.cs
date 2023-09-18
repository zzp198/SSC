using Terraria;
using Terraria.ModLoader;

namespace SSC.Core;

public class MarkSystem : ModSystem
{
    internal static bool AnyActiveDanger; // 只统计boss,忽略四柱和火星探测器.

    internal uint MarkCountdown;

    public override void PostUpdateNPCs()
    {
        if (MarkCountdown == 0)
        {
            AnyActiveDanger = false;
            foreach (var n in Main.npc)
            {
                if (n.active && n.boss)
                {
                    AnyActiveDanger = true;
                    break;
                }
            }
        }
    }

    public override void PostUpdateEverything()
    {
        MarkCountdown++;
        if (MarkCountdown > 60)
        {
            MarkCountdown = 0;
        }
    }
}