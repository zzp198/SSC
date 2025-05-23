﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // SSC/([MapID:xxx-x-xxx])/[SteamID:0-9]/zzp198.plr
    public static string PATH => Path.Combine(Main.SavePath, nameof(SSC));

    public static string MapID => ModContent.GetInstance<ServerConfig>().Save4World ? $"{Main.ActiveWorldFileData.UniqueId}" : "";

    public static string Password = "";

    public static string CachedPID = "";

    public static string GetPID()
    {
        if (CachedPID == "")
        {
            try
            {
                CachedPID = SteamUser.GetSteamID().m_SteamID.ToString();
            }
            catch (Exception e)
            {
                ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral(e.ToString()), Color.Red, Main.myPlayer);
                ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Mods.SSC.SteamIDWrong"), Color.Red,
                    Main.myPlayer);
                CachedPID = Main.clientUUID;
            }
        }

        return CachedPID;
    }

    public override void Load()
    {
        Main.SettingsUnlock_WorldEvil = true; // 解锁自选腐化猩红
    }

    public override void PostSetupContent()
    {
        if (Main.dedServ)
        {
            Password = new Random().Next(0, 1000000).ToString("D6");
            ModContent.GetInstance<Commands.AdminCode>().Action(null, null, null);
        }
    }

    public override void HandlePacket(BinaryReader reader, int from)
    {
        var msg = reader.ReadByte();
        switch ((MessageID)msg)
        {
            case MessageID.MessageSegment:
            {
                MessageManager.ProcessMessage(reader, from);
                break;
            }
            case MessageID.SaveSSC:
            {
                //只有服务端会执行,from必为客户端的id
                var id = reader.ReadString();
                var name = reader.ReadString();
                var data = reader.ReadBytes(reader.ReadInt32());
                var root = TagIO.Read(reader);
                var first = reader.ReadBoolean();
                var showsavedmassage = ModContent.GetInstance<ServerConfig>().ShowSavedMassage;

                // DisplayMessageOnClient固定发送方为服务端,服务端->客户端.
                // SendChatMessageToClient可以自定义发送方,客户端->服务端->客户端.且发送方不会显示.
                if (string.IsNullOrWhiteSpace(name))
                {
                    ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Net.EmptyName"), Color.Red, from);
                    return;
                }

                if (name.Length > 16) // SteamID是17位,这样他们永远不会和初始化的鬼魂角色名称相同.
                {
                    ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Net.NameTooLong"), Color.Red, from);
                    return;
                }

                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Mods.SSC.NameError"), Color.Red, from);
                    return;
                }

                switch (first)
                {
                    // 需在保存的“根目录”深度查询所有世界的角色,避免新角色出现重名.
                    case true when Directory.GetFiles(PATH, $"{name}.plr", SearchOption.AllDirectories).Any():
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromKey(Lang.mp[5].Key, name), Color.Red, from);
                        return;
                    }
                    // 保存时必须源文件存在,避免出现硬核回档的情况.
                    case false when !File.Exists(Path.Combine(PATH, MapID, id, $"{name}.plr")):
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral("保存失败，存档不存在！"), Color.Red, from);
                        return;
                    }
                }

                Utils.TryCreatingDirectory(Path.Combine(PATH, MapID, id));

                File.WriteAllBytes(Path.Combine(PATH, MapID, id, $"{name}.plr"), data);
                TagIO.ToFile(root, Path.Combine(PATH, MapID, id, $"{name}.tplr"));

                var stream = new MemoryStream();
                TagIO.ToStream(root, stream);
                stream.Flush();

                if (showsavedmassage)
                {
                    // 保存成功时在客户端输出消息
                    var KB = (data.LongLength + stream.Length) / 1024.0;
                    var size = $"[c/{(KB < 64 ? Color.Green.Hex3() : Color.Yellow.Hex3())}:{KB:N2} KB]";
                    var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Mods.SSC.SaveSuccessful", size, time),
                        Color.Green, from);
                }

                if (first)
                {
                    NetMessage.TrySendData(Terraria.ID.MessageID.WorldData, from);
                }

                break;
            }
            case MessageID.GoGoSSC:
            {
                //双向通信,客户端->服务端->客户端
                if (Main.netMode == NetmodeID.Server)
                {
                    var id = reader.ReadString();
                    var name = reader.ReadString();

                    // 登录时也进行一次校验
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Net.EmptyName"), Color.Red, from);
                        return;
                    }

                    if (name.Length > 16) // SteamID是17位,这样他们永远不会和初始化的鬼魂角色名称相同.
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Net.NameTooLong"), Color.Red, from);
                        return;
                    }

                    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Mods.SSC.NameError"), Color.Red, from);
                        return;
                    }
                    
                    if (ModContent.GetInstance<ServerConfig>().DontReviveWhenBossFight && MarkSystem.AnyActiveDanger)
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromKey("Mods.SSC.DontReviveWhenBossFight"),
                            Color.Red, from);
                        return;
                    }

                    if (Netplay.Clients.Where(c => c.IsActive).Any(c => Main.player[c.Id].name == name)) // 防止同名在线
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromKey(Lang.mp[5].Key, name), Color.Red, from);
                        return;
                    }

                    var file_data = Player.LoadPlayer(Path.Combine(PATH, MapID, id, $"{name}.plr"), false);
                    // 进行基础的校验,如禁止旅行角色进入非旅行世界,以及其他mod进入限制.
                    if (file_data.Player.difficulty == PlayerDifficultyID.Creative && !Main.GameModeInfo.IsJourneyMode)
                    {
                        ChatHelper.DisplayMessageOnClient(
                            NetworkText.FromKey("Net.PlayerIsCreativeAndWorldIsNotCreative"), Color.Red, from);
                        return;
                    }

                    if (file_data.Player.difficulty != PlayerDifficultyID.Creative && Main.GameModeInfo.IsJourneyMode)
                    {
                        ChatHelper.DisplayMessageOnClient(
                            NetworkText.FromKey("Net.PlayerIsNotCreativeAndWorldIsCreative"), Color.Red, from);
                        return;
                    }

                    if (!SystemLoader.CanWorldBePlayed(file_data, Main.ActiveWorldFileData, out var mod))
                    {
                        var message = mod.WorldCanBePlayedRejectionMessage(file_data, Main.ActiveWorldFileData);
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral(message), Color.Red, from);
                        return;
                    }

                    // 登录端Client挂载全部数据.(发送给全部Client挂载会出现显示错误,会导致优先Spawn造成数据和画面的短暂不同步,且其他客户端没有必要知道全部数据.)
                    var data = File.ReadAllBytes(Path.Combine(PATH, MapID, id, $"{name}.plr"));
                    var root = TagIO.FromFile(Path.Combine(PATH, MapID, id, $"{name}.tplr"));

                    var mp = ModContent.GetInstance<SSC>().GetPacket();
                    mp.Write((byte)MessageID.GoGoSSC);
                    mp.Write(data.Length);
                    mp.Write(data);
                    TagIO.Write(root, mp);
                    MessageManager.FrameSend(mp, from);

                    // 这里需要确保先收到全部Message并挂载,再收到PlayerInfo来同步信息.
                    // 根据客户端的挂载数据来同步到服务端,不添加的话,离开时的提示信息有误且后进的玩家无法被先进的玩家看到(虽然死亡能解除)
                    NetMessage.SendData(Terraria.ID.MessageID.PlayerInfo, from);

                    return;
                }

                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    var data = reader.ReadBytes(reader.ReadInt32());
                    var root = TagIO.Read(reader);

                    var memoryStream = new MemoryStream();
                    TagIO.ToStream(root, memoryStream);

                    // 需要修改file_data的Path为SSC以开启云存档,同时注意继承PlayTime,否则新档会丢失游玩时间.
                    var file_data = new PlayerFileData(Path.Combine(Main.PlayerPath, $"{GetPID()}.SSC"), false)
                    {
                        Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
                    };
                    // data中包含playtime并且会添加到file_data里
                    Player.LoadPlayerFromStream(file_data, data, memoryStream.ToArray());
                    file_data.MarkAsServerSide();
                    file_data.SetAsActive();

                    file_data.Player.Spawn(PlayerSpawnContext.SpawningIntoWorld);
                    try
                    {
                        Player.Hooks.EnterWorld(Main.myPlayer); // 其他mod如果没有防御性编程可能会报错
                    }
                    catch (Exception e)
                    {
                        ChatHelper.DisplayMessageOnClient(NetworkText.FromLiteral(e.ToString()), Color.Red,
                            Main.myPlayer);
                    }
                    finally
                    {
                        ModContent.GetInstance<ServerSystem>().UI?.SetState(null);
                    }
                }

                break;
            }
            case MessageID.EraseSSC:
            {
                //只有服务端会执行,from必为客户端的id
                var id = reader.ReadString();
                var name = reader.ReadString();

                if (File.Exists(Path.Combine(PATH, MapID, id, $"{name}.plr")))
                {
                    File.Delete(Path.Combine(PATH, MapID, id, $"{name}.plr"));
                    File.Delete(Path.Combine(PATH, MapID, id, $"{name}.tplr"));
                    ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.SSC.EraseSuccessful"), Color.Yellow,
                        from);
                }

                NetMessage.TrySendData(Terraria.ID.MessageID.WorldData, from);
                break;
            }
            case MessageID.ClientModCheck:
            {
                List<string> AllowedClientMods = [];
                AllowedClientMods.AddRange(from mod in ModLoader.Mods where mod.Side == ModSide.Client select mod.Name);
                AllowedClientMods.AddRange(ModContent.GetInstance<ServerConfig>().AllowedClientMods);

                List<string> IClientModes = [];

                var num = reader.ReadInt32();
                for (var i = 0; i < num; i++)
                {
                    var name = reader.ReadString();
                    if (!AllowedClientMods.Contains(name))
                    {
                        IClientModes.Add(name);
                    }
                }

                if (IClientModes.Count > 0)
                {
                    var names = string.Join(", ", IClientModes);
                    NetMessage.BootPlayer(from, NetworkText.FromKey("Mods.SSC.IClientMod", names));
                }

                break;
            }
            default:
            {
                switch (Main.netMode)
                {
                    case NetmodeID.MultiplayerClient:
                        Netplay.Disconnect = true;
                        Main.statusText = $"Unexpected message id: {msg}";
                        Main.menuMode = 14;
                        break;
                    case NetmodeID.Server:
                        NetMessage.BootPlayer(from, NetworkText.FromLiteral($"Unexpected message id: {msg}"));
                        break;
                }

                break;
            }
        }
    }
}

public enum MessageID : byte
{
    MessageSegment, // 超过65536的包进行分片传输

    SaveSSC, // 客户端->服务端,id,name,data,root,first

    GoGoSSC, // 客户端->服务端,id,name.服务端->客户端,data,root.

    EraseSSC, // 客户端->服务端,id,name

    ClientModCheck, // 客户端->服务端,num,name
}
