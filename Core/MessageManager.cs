using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Terraria.Chat;
using Terraria.Localization;
using Terraria.ModLoader;

namespace SSC.Core;

public class MessageManager : ModSystem
{
    internal static Dictionary<int, byte[]> MessageSegment;

    public override void Load()
    {
        MessageSegment = new Dictionary<int, byte[]>();
    }

    public static void SendMessage(ModPacket root, int to = -1, int ignore = -1)
    {
        if (root.BaseStream.Position < 60000)
        {
            root.Send(to, ignore);
        }
        else
        {
            // My message -> 01-09-08, True message -> [XX-XX]-[FA]-[ID]-01-09-08
            // [XX-XX]-[FA]-[ID]-XX-XX-XX-XX-Position-[XX-XX]-[FA]-[ID]-01-09-08
            var data = ((MemoryStream)root.BaseStream).ToArray(); // 此时封装的Stream除了自身的数据外,开头还会有4字节的标志,以及更早的其他合并的数据流
            var hash = Convert.ToHexString(MD5.Create().ComputeHash(data)); // 将Stream原封不动的传输过去

            var mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)MessageID.MessageSegment);
            mp.Write(true);
            mp.Send(to, ignore);

            var memoryStream = new MemoryStream(data);
            var io = new byte[16384];
            int length;
            for (; (length = memoryStream.Read(io, 0, io.Length)) > 0;)
            {
                mp = ModContent.GetInstance<SSC>().GetPacket();
                mp.Write((byte)MessageID.MessageSegment);
                mp.Write(false);
                mp.Write(false);
                mp.Write(length);
                mp.Write(io, 0, length);
                mp.Send(to, ignore);
            }

            mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)MessageID.MessageSegment);
            mp.Write(false);
            mp.Write(true);
            mp.Write(hash);
            mp.Send(to, ignore);

            root.Close();
        }
    }

    public static void ReceiveMessage(BinaryReader reader, int from)
    {
        if (reader.ReadBoolean())
        {
            MessageSegment[from] = Array.Empty<byte>();
            return;
        }

        if (reader.ReadBoolean())
        {
            var hash = reader.ReadString();
            var data = MessageSegment[from];
            if (Convert.ToHexString(MD5.Create().ComputeHash(data.ToArray())) != hash)
            {
                ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral("Hash校验失败,信息将被丢弃!"), Color.Red, from);
                return;
            }

            // Message本身是完整的一段PacketStream,所以也包括前面的4字节TML自带的标识头.
            ModContent.GetInstance<SSC>().HandlePacket(new BinaryReader(new MemoryStream(data[4..])), from);
            return;
        }

        var root = reader.ReadBytes(reader.ReadInt32());
        MessageSegment[from] = MessageSegment[from].Concat(root).ToArray();
    }

    public override void Unload()
    {
        MessageSegment = null;
    }
}