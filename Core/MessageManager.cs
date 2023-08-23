using System.Collections.Generic;
using Terraria.ModLoader;

namespace SSC.Core;

public class MessageManager : ModSystem
{
    public List<(byte[] data, string hash)> MessageSegment;

    public override void OnModLoad()
    {
        MessageSegment = new List<(byte[] data, string hash)>(255);
    }

    public void SendMessage(ModPacket mp, int to, int from)
    {
    }

    public override void OnModUnload()
    {
        MessageSegment = null;
    }
}