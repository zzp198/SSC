using System;
using System.IO;
using Terraria.ModLoader;

namespace SSC;

public class TrashPlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        var packet = Mod.GetPacket();
        packet.Write((byte)99);
        packet.Write(new byte[100000]);
        packet.Write(198);
        packet.Write(198);
        SSC.StreamPatcher(packet);
    }
}