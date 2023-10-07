using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Terraria.ModLoader;

namespace TryFixPacketTooLarge;

public class TryFixPacketTooLarge : Mod
{
    internal static ConcurrentDictionary<int, byte[]> MessageSegment;
    // private object _locker = new();

    public TryFixPacketTooLarge()
    {
        var target = typeof(ModPacket).GetMethod(nameof(ModPacket.Send), BindingFlags.Instance | BindingFlags.Public);
        if (target == null)
        {
            Logger.Error("Try Fix Packet Too Large error, ModPacket.Send is null.");
            return;
        }

        MonoModHooks.Add(target, new HookModPacketSend(BeforePacketSend));
    }

    private delegate void OrigModPacketSend(ModPacket self, int toClient = -1, int ignoreClient = -1);

    private delegate void HookModPacketSend(OrigModPacketSend orig, ModPacket self, int toClient = -1,
        int ignoreClient = -1);

    void BeforePacketSend(OrigModPacketSend orig, ModPacket self, int toClient = -1, int ignoreClient = -1)
    {
        if (self == null) return;
        if (self.BaseStream.Position < 60000)
        {
            orig(self, toClient, ignoreClient);
        }
        else
        {
            var thread = new Thread(() => BeforePacketSendInner(self, toClient, ignoreClient));
            thread.Start();
        }
    }

    void BeforePacketSendInner(ModPacket self, int toClient = -1, int ignoreClient = -1)
    {
        // lock (_locker)
        // {
        // My message -> 01-09-08, True message -> [XX-XX]-[FA]-[ID]-01-09-08
        // [XX-XX]-[FA]-[ID]-XX-XX-XX-XX-Position-[XX-XX]-[FA]-[ID]-01-09-08
        var data = ((MemoryStream)self.BaseStream).ToArray(); // 此时封装的Stream除了自身的数据外,开头还会有4字节的标志,以及更早的其他合并的数据流
        var hash = Convert.ToHexString(MD5.Create().ComputeHash(data)); // 将Stream原封不动的传输过去

        var mp = ModContent.GetInstance<TryFixPacketTooLarge>().GetPacket();
        mp.Write(true);
        mp.Send(toClient, ignoreClient);

        var memoryStream = new MemoryStream(data);
        var io = new byte[16384];
        int length;
        for (; (length = memoryStream.Read(io, 0, io.Length)) > 0;)
        {
            mp = ModContent.GetInstance<TryFixPacketTooLarge>().GetPacket();
            mp.Write(false);
            mp.Write(false);
            mp.Write(length);
            mp.Write(io, 0, length);
            mp.Send(toClient, ignoreClient);
        }

        mp = ModContent.GetInstance<TryFixPacketTooLarge>().GetPacket();
        mp.Write(false);
        mp.Write(true);
        mp.Write(GetNetID(self));
        mp.Write(hash);
        mp.Send(toClient, ignoreClient);
        // }
    }

    short GetNetID(ModPacket mp)
    {
        var id = mp.GetType().GetField("netID", BindingFlags.Instance | BindingFlags.NonPublic);
        if (id == null)
        {
            return -1;
        }

        var obj = id.GetValue(mp);
        if (obj == null)
        {
            return -1;
        }

        return (short)obj;
    }

    public override void HandlePacket(BinaryReader reader, int from)
    {
        if (reader.ReadBoolean())
        {
            MessageSegment[from] = Array.Empty<byte>();
            return;
        }

        if (reader.ReadBoolean())
        {
            var id = reader.ReadInt16();
            var hash = reader.ReadString();
            if (id == -1)
            {
                Logger.Warn("Which mod send this?");
                return;
            }

            var data = MessageSegment[from];
            if (Convert.ToHexString(MD5.Create().ComputeHash(data.ToArray())) != hash)
            {
                Logger.Warn("Hash check error!");
                return;
            }

            // Message本身是完整的一段PacketStream,所以也包括前面的4字节TML自带的标识头.
            ModNet.GetMod(id).HandlePacket(new BinaryReader(new MemoryStream(data[4..])), from);
            return;
        }

        var root = reader.ReadBytes(reader.ReadInt32());
        MessageSegment[from] = MessageSegment[from].Concat(root).ToArray();
    }

    public override void Load()
    {
        MessageSegment = new ConcurrentDictionary<int, byte[]>();
    }

    public override void Unload()
    {
        MessageSegment = null;
    }
}