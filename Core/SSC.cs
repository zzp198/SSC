using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Steamworks;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SSC.Core;

public class SSC : Mod
{
    internal readonly static string PATH = Path.Combine(Main.SavePath, nameof(SSC));
    internal readonly static Dictionary<int, byte[]> Stream = new();

    public override void Load()
    {
        for (var i = 0; i <= byte.MaxValue; i++)
        {
            Stream[i] = Array.Empty<byte>();
        }
    }

    public override void HandlePacket(BinaryReader reader, int from)
    {
        var msg_id = reader.ReadByte();
        switch ((MsgID)msg_id)
        {
            case MsgID.Stream:
            {
                if (reader.ReadBoolean())
                {
                    Stream[from] = Array.Empty<byte>();
                    break;
                }

                if (reader.ReadBoolean())
                {
                    var hash = reader.ReadString();
                    var data = Stream[from];
                    if (Convert.ToHexString(MD5.Create().ComputeHash(data)) != hash)
                    {
                        Console.WriteLine("Hash校验失败");
                    }

                    // data本身是完整的一段PacketStream,当然也包括前面的4字节标志
                    HandlePacket(new BinaryReader(new MemoryStream(data[4..])), from);
                    break;
                }

                var binary = reader.ReadBytes(reader.ReadInt32());
                Stream[from] = Stream[from].Concat(binary).ToArray();

                break;
            }
            case MsgID.TrySave:
            {
                // 只有服务端会执行,from即为客户端的id
                var id = reader.ReadString();
                var name = reader.ReadString();
                var data = reader.ReadBytes(reader.ReadInt32());
                var tag = TagIO.Read(reader);
                var first = reader.ReadBoolean();

                if (string.IsNullOrWhiteSpace(name))
                {
                    ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Net.EmptyName"), Color.Red, from);
                    return;
                }

                if (name.Length > 16) // SteamID是17位,这样他们永远不会和初始化的角色名称相同.
                {
                    ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Net.NameTooLong"), Color.Red, from);
                    return;
                }

                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    // TODO
                    ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral("无效的角色昵称,请不要添加一些奇奇怪怪的东西"), Color.Red, from);
                    return;
                }


                var dir = SSC.PATH;
                // if (ModContent.GetInstance<Configs.ServerConfig>().EveryWorld)
                // {
                //     dir = Path.Combine(dir, Main.ActiveWorldFileData.UniqueId.ToString());
                // }

                if (first && Directory.GetFiles(dir, $"{name}.plr", SearchOption.AllDirectories).Length > 0) // 深度搜索,防止不同玩家间同名注册
                {
                    ChatHelper.SendChatMessageToClient(NetworkText.FromKey(Lang.mp[5].Key, name), Color.Red, from);
                    return;
                }

                if (!first && !File.Exists(Path.Combine(dir, id, $"{name}.plr"))) // 保存时必须源文件存在,避免硬核回档的情况
                {
                    // TODO
                    ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral("保存失败,文件不存在/已删除"), Color.Red, from);
                    return;
                }

                Utils.TryCreatingDirectory(Path.Combine(dir, id));

                File.WriteAllBytes(Path.Combine(dir, id, $"{name}.plr"), data);
                TagIO.ToFile(tag, Path.Combine(dir, id, $"{name}.tplr"));

                // if (!first && ModContent.GetInstance<Configs.ServerConfig>().Tip)
                // {
                //     // TODO
                //     ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral("Save successful. 保存成功."), Color.Yellow, from);
                // }

                if (first)
                {
                    // TODO
                    ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral("Create successful. 创建成功"), Color.Yellow, from);
                    NetMessage.TrySendData(MessageID.WorldData, from);
                }

                break;
            }
            case MsgID.TryLoad:
            {
                if (Main.netMode == NetmodeID.Server)
                {
                    var id = reader.ReadString();
                    var name = reader.ReadString();

                    if (Netplay.Clients.Where(c => c.IsActive).Any(c => Main.player[c.Id].name == name)) // 防止同名在线
                    {
                        ChatHelper.SendChatMessageToClient(NetworkText.FromKey(Lang.mp[5].Key, name), Color.Red, from);
                        return;
                    }

                    var dir = SSC.PATH;
                    // if (ModContent.GetInstance<Configs.ServerConfig>().EveryWorld)
                    // {
                    //     dir = Path.Combine(dir, Main.ActiveWorldFileData.UniqueId.ToString());
                    // }

                    var file_data = Player.LoadPlayer(Path.Combine(dir, id, $"{name}.plr"), false);
                    if (file_data.Player.difficulty == PlayerDifficultyID.Creative && !Main.GameModeInfo.IsJourneyMode)
                    {
                        ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Net.PlayerIsCreativeAndWorldIsNotCreative"), Color.Red, from);
                        return;
                    }

                    if (file_data.Player.difficulty != PlayerDifficultyID.Creative && Main.GameModeInfo.IsJourneyMode)
                    {
                        ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Net.PlayerIsNotCreativeAndWorldIsCreative"), Color.Red, from);
                        return;
                    }

                    if (!SystemLoader.CanWorldBePlayed(file_data, Main.ActiveWorldFileData, out var mod)) // 兼容其他mod的游玩规则
                    {
                        var message = mod.WorldCanBePlayedRejectionMessage(file_data, Main.ActiveWorldFileData);
                        ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(message), Color.Red, from);
                        return;
                    }

                    // 指定Client挂载全部数据,不管是否需要同步的,以确保mod的本地数据同步.(发送给全部Client会出现显示错误,会先Spawn)
                    var data = File.ReadAllBytes(Path.Combine(dir, id, $"{name}.plr"));
                    var tag = TagIO.FromFile(Path.Combine(dir, id, $"{name}.tplr"));

                    var mp = ModContent.GetInstance<SSC>().GetPacket();
                    mp.Write((byte)MsgID.TryLoad);
                    mp.Write(data.Length);
                    mp.Write(data);
                    TagIO.Write(tag, mp);
                    SSC.StreamPatcher(mp, from);

                    // 客户端的返回数据会更改服务端的Client名称,不添加的话,离开时的提示信息有误且后进的玩家无法被先进的玩家看到(虽然死亡能解除)
                    NetMessage.SendData(MessageID.PlayerInfo, from);

                    return;
                }

                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    var data = reader.ReadBytes(reader.ReadInt32());
                    var tag = TagIO.Read(reader);

                    var memoryStream = new MemoryStream();
                    TagIO.ToStream(tag, memoryStream);

                    var binary = $"SSC@{Convert.ToHexString(data)}@{Convert.ToHexString(memoryStream.ToArray())}@.plr";
                    var remote_file_data = Player.LoadPlayer(binary, false);
                    // 需要修改file_data的Path,同时注意继承PlayTime.
                    var file_data = new PlayerFileData(Path.Combine(Main.PlayerPath, $"{SteamUser.GetSteamID().m_SteamID}.plr"), false)
                    {
                        Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
                        Player = remote_file_data.Player
                    };
                    file_data.SetPlayTime(remote_file_data.GetPlayTime());
                    file_data.MarkAsServerSide();
                    file_data.SetAsActive();

                    file_data.Player.Spawn(PlayerSpawnContext.SpawningIntoWorld); // SetPlayerDataToOutOfClassFields,设置临时物品
                    try
                    {
                        Player.Hooks.EnterWorld(Main.myPlayer); // 其他mod如果没有防御性编程可能会报错
                    }
                    catch (Exception e)
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral(e.ToString()), Color.Red, Main.myPlayer);
                        throw;
                    }
                    finally
                    {
                        Systems.ViewSystem.View.SetState(null);
                    }
                }

                break;
            }
            case MsgID.TryRemove:
            {
                var id = reader.ReadString();
                var name = reader.ReadString();

                var dir = SSC.PATH;
                // if (ModContent.GetInstance<Configs.ServerConfig>().EveryWorld)
                // {
                //     dir = Path.Combine(dir, Main.ActiveWorldFileData.UniqueId.ToString());
                // }

                if (File.Exists(Path.Combine(dir, id, $"{name}.plr")))
                {
                    File.Delete(Path.Combine(dir, id, $"{name}.plr"));
                    File.Delete(Path.Combine(dir, id, $"{name}.tplr"));

                    // TODO
                    ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral("Delete successful. 删除成功"), Color.Yellow, from);

                    NetMessage.TrySendData(MessageID.WorldData, from);
                }
                else
                {
                    ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral("Delete failed. 删除失败"), Color.Yellow, from);
                }

                break;
            }
            default:
            {
                switch (Main.netMode)
                {
                    case NetmodeID.MultiplayerClient:
                        Netplay.Disconnect = true;
                        Main.statusText = $"Unexpected message id: {msg_id}";
                        Main.menuMode = 14;
                        break;
                    case NetmodeID.Server:
                        NetMessage.BootPlayer(from, NetworkText.FromLiteral($"Unexpected message id: {msg_id}"));
                        break;
                }

                break;
            }
        }
    }

    public static void StreamPatcher(ModPacket binary, int toClient = -1, int ignoreClient = -1)
    {
        if (binary.BaseStream.Position < 60000)
        {
            binary.Send(toClient, ignoreClient);
        }
        else
        {
            // My message -> 01-09-08, True message -> [XX-XX]-[FA]-[01]-01-09-08
            // [XX-XX]-[FA]-[01]-XX-XX-XX-XX-Position-[XX-XX]-[FA]-[01]-01-09-08
            var data = ((MemoryStream)binary.BaseStream).ToArray(); // 此时封装的Stream除了自身的数据外,开头还会有4字节的标志,以及更早的其他数据流
            var hash = Convert.ToHexString(MD5.Create().ComputeHash(data)); // 将Stream原封不动的传输过去

            var mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)MsgID.Stream);
            mp.Write(true);
            mp.Send(toClient, ignoreClient);

            var memoryStream = new MemoryStream(data);
            var io = new byte[16384];
            int length;
            while ((length = memoryStream.Read(io, 0, io.Length)) > 0)
            {
                mp = ModContent.GetInstance<SSC>().GetPacket();
                mp.Write((byte)MsgID.Stream);
                mp.Write(false);
                mp.Write(false);
                mp.Write(length);
                mp.Write(io, 0, length);
                mp.Send(toClient, ignoreClient);
            }

            mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)MsgID.Stream);
            mp.Write(false);
            mp.Write(true);
            mp.Write(hash);
            mp.Send(toClient, ignoreClient);

            binary.Close();
        }
    }
}