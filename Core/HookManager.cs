﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Steamworks;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.Utilities;

namespace SSC.Core;

public class HookManager : ModSystem
{
    public override void Load()
    {
        IL_MessageBuffer.GetData += ILHook0;
        IL_NetMessage.SendData += ILHook1;
        IL_MessageBuffer.GetData += ILHook2;
        IL_Main.DrawInterface += ILHook3;
        On_FileUtilities.Exists += OnHook1;
        On_FileUtilities.ReadAllBytes += OnHook2;
        On_Player.InternalSavePlayerFile += OnHook3;
        On_Player.KillMeForGood += OnHook4;
    }

    void ILHook0(ILContext il)
    {
        // Main.ServerSideCharacter = true;
        var cur = new ILCursor(il);
        cur.GotoNext(MoveType.Before, i => i.MatchStsfld<Main>(nameof(Main.ServerSideCharacter)));
        cur.EmitDelegate<Func<bool, bool>>(_ => true);
    }

    // 服务端在客户端连接时发送游戏模式.
    void ILHook1(ILContext il)
    {
        // case 3:
        //    writer.Write((byte) remoteClient);
        //    writer.Write(false);
        // -> writer.Write((byte) Main.GameMode);
        //    break;
        var cur = new ILCursor(il);
        cur.GotoNext(MoveType.After,
            i => i.MatchLdloc(3), i => i.MatchLdarg(1), i => i.MatchConvU1(),
            i => i.MatchCallvirt(typeof(BinaryWriter), nameof(BinaryWriter.Write)),
            i => i.MatchLdloc(3), i => i.MatchLdcI4(0),
            i => i.MatchCallvirt(typeof(BinaryWriter), nameof(BinaryWriter.Write))
        );
        cur.Emit(OpCodes.Ldloc_3);
        cur.EmitDelegate<Action<BinaryWriter>>(i => i.Write((byte)Main.GameMode));
    }

    // 客户端根据发送过来的游戏模式进行初始化,同时意味这只有在多人模式中才会初始化和选择角色.
    void ILHook2(ILContext il)
    {
        //    if (Netplay.Connection.State == 1)
        //        Netplay.Connection.State = 2;
        //    int index1 = (int) this.reader.ReadByte();
        //    bool flag1 = this.reader.ReadBoolean();
        // -> byte gameMode = this.reader.ReadByte();
        var cur = new ILCursor(il);
        cur.GotoNext(MoveType.After,
            i => i.MatchLdarg(0),
            i => i.MatchLdfld(typeof(MessageBuffer), nameof(MessageBuffer.reader)),
            i => i.MatchCallvirt(typeof(BinaryReader), nameof(BinaryReader.ReadByte)),
            i => i.MatchStloc(out _),
            i => i.MatchLdarg(0),
            i => i.MatchLdfld(typeof(MessageBuffer), nameof(MessageBuffer.reader)),
            i => i.MatchCallvirt(typeof(BinaryReader), nameof(BinaryReader.ReadBoolean)),
            i => i.MatchStloc(out _)
        );
        cur.Emit(OpCodes.Ldarg_0);
        cur.Emit(OpCodes.Ldfld, typeof(MessageBuffer).GetField(nameof(MessageBuffer.reader))!);
        cur.EmitDelegate<Action<BinaryReader>>(i =>
        {
            var game_mode = i.ReadByte();
            if (Netplay.Connection.State != 2)
            {
                return; // 通过hook上面的条件可实现仅在每次初次进入世界时初始化
            }

            // UUID会影响本地的map数据,同一个UUID的不同角色会拥有相同的地图探索
            var m_SteamID = SteamUser.GetSteamID().m_SteamID.ToString();
            var fileData = new PlayerFileData(Path.Combine(Main.PlayerPath, $"{m_SteamID}.plr"), false)
            {
                Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
                Player = new Player
                {
                    name = m_SteamID, difficulty = game_mode,
                    // MessageID -> StatLife:16  Ghost:13  Dead:12&16
                    statLife = 0, statMana = 0, dead = true, ghost = true,
                    // 避免因为进入世界的自动复活,导致客户端与服务端失去同步
                    respawnTimer = int.MaxValue, lastTimePlayerWasSaved = long.MaxValue,
                    savedPerPlayerFieldsThatArentInThePlayerClass = new Player.SavedPlayerDataWithAnnoyingRules()
                }
            };
            // 正常情况下不会拥有此标记,区别与Main.SSC,这个只会影响本地角色的保存,不会更改游戏流程
            fileData.MarkAsServerSide();
            fileData.SetAsActive();

            ModContent.GetInstance<ServerSystem>().UI?.SetState(new ServerViewer()); // 唯一设置界面的地方
        });
    }

    // SSC界面下禁止其他MOD的界面和操作
    void ILHook3(ILContext il)
    {
        var cur = new ILCursor(il);
        cur.GotoNext(MoveType.After,
            i => i.MatchCall(typeof(SystemLoader), nameof(SystemLoader.ModifyInterfaceLayers)));
        cur.EmitDelegate<Func<List<GameInterfaceLayer>, List<GameInterfaceLayer>>>(layers =>
        {
            if (ModContent.GetInstance<ServerSystem>().UI?.CurrentState != null)
            {
                layers.ForEach(layer => layer.Active = layer.Name switch
                {
                    "Vanilla: Map / Minimap" => false,
                    "Vanilla: Resource Bars" => false,
                    _ => layer.Name.StartsWith("Vanilla:")
                });
            }

            return layers;
        });
    }

    // 放行特定的数据格式
    bool OnHook1(On_FileUtilities.orig_Exists orig, string path, bool cloud)
    {
        if (path == null)
        {
            return false;
        }

        return path.StartsWith("SSC@") || orig(path, cloud);
    }

    // 原本的path后缀为plr或tplr,根据后缀在特定格式的数据中返回内容
    byte[] OnHook2(On_FileUtilities.orig_ReadAllBytes orig, string path, bool cloud)
    {
        if (path.StartsWith("SSC@"))
        {
            var regex = new Regex(@"^SSC@(?<plr>[0-9A-F]+)@(?<tplr>[0-9A-F]+)@\.(?<type>plr|tplr)$");
            var match = regex.Match(path);
            if (match.Success)
            {
                return Convert.FromHexString(match.Groups[match.Groups["type"].Value].Value);
            }

            throw new ArgumentException("SSC regex match failed.");
        }

        return orig(path, cloud);
    }

    // 只有路径为.SSC结尾的SSC存档才会保存到云端,只需要控制SSC标志和路径后缀即可轻松区分.
    // 路径有三种,PS/[SteamID].plr,Create.SSC,PS/[SteamID].SSC
    void OnHook3(On_Player.orig_InternalSavePlayerFile orig, PlayerFileData fileData)
    {
        // 不属于原版的内容,没法使用TerraHook,但是依旧不会对此接口造成破坏,庆幸.
        if (fileData.ServerSideCharacter && fileData.Path.EndsWith("SSC"))
        {
            try
            {
                var plr = GetPLR(fileData);
                var tplr = GetTPLR(fileData);

                var mp = Mod.GetPacket();
                mp.Write((byte)MessageID.SaveSSC);
                mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
                mp.Write(fileData.Player.name);
                mp.Write(plr.Length);
                mp.Write(plr);
                TagIO.Write(tplr, mp);
                mp.Write(fileData.Path == "Create.SSC");
                MessageManager.FrameSend(mp);
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e);
            }

            return;
        }

        orig(fileData);
    }

    byte[] ENC_KEY = (byte[])typeof(Player).GetField("ENCRYPTION_KEY", (BindingFlags)40)!.GetValue(null);
    MethodInfo PlayerSerialize = typeof(Player).GetMethod("Serialize", (BindingFlags)40);

    byte[] GetPLR(PlayerFileData fileData)
    {
        var rijndaelManaged = new RijndaelManaged();
        using (var stream = new MemoryStream(2000))
        {
            using (var output = new CryptoStream(stream, rijndaelManaged.CreateEncryptor(ENC_KEY, ENC_KEY),
                       CryptoStreamMode.Write))
            {
                using (var binaryWriter = new BinaryWriter(output))
                {
                    PlayerLoader.PreSavePlayer(fileData.Player);
                    binaryWriter.Write(279);
                    fileData.Metadata.Write(binaryWriter);
                    PlayerSerialize.Invoke(null, new object[] { fileData, fileData.Player, binaryWriter });
                    binaryWriter.Flush();
                    output.FlushFinalBlock();
                    stream.Flush();
                    PlayerLoader.PostSavePlayer(fileData.Player);
                    return stream.ToArray();
                }
            }
        }
    }

    TagCompound GetTPLR(PlayerFileData fileData)
    {
        var SaveData =
            Assembly.Load("tModLoader").GetType("Terraria.ModLoader.IO.PlayerIO")!.GetMethod("SaveData",
                (BindingFlags)40);
        return (TagCompound)SaveData!.Invoke(null, new object[] { fileData.Player });
    }

    // 硬核死亡删除云存档
    void OnHook4(On_Player.orig_KillMeForGood orig, Player player)
    {
        var fileData = Main.ActivePlayerFileData;
        if (fileData.ServerSideCharacter)
        {
            var mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)MessageID.EraseSSC);
            mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
            mp.Write(fileData.Player.name);
            mp.Send();
        }

        orig(player);
    }
}