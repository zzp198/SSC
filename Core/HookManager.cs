using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        IL_NetMessage.SendData += ILHook1;
        IL_MessageBuffer.GetData += ILHook2;
        IL_Main.DrawInterface += ILHook3;
        On_FileUtilities.Exists += OnHook1;
        On_FileUtilities.ReadAllBytes += OnHook2;
        IL_Player.InternalSavePlayerFile += ILHook4;
        On_Player.KillMeForGood += OnHook3;
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
            var file_data = new PlayerFileData(Path.Combine(Main.PlayerPath, $"{SteamUser.GetSteamID().m_SteamID}.plr"),
                false)
            {
                Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
                Player = new Player
                {
                    name = SteamUser.GetSteamID().m_SteamID.ToString(), difficulty = game_mode,
                    // MessageID -> StatLife:16  Ghost:13  Dead:12&16
                    statLife = 0, statMana = 0, dead = true, ghost = true,
                    // 避免因为进入世界的自动复活,导致客户端与服务端失去同步
                    respawnTimer = int.MaxValue, lastTimePlayerWasSaved = long.MaxValue,
                    savedPerPlayerFieldsThatArentInThePlayerClass = new Player.SavedPlayerDataWithAnnoyingRules()
                }
            };
            // 正常情况下不会拥有此标记,区别与Main.SSC,这个只会影响本地角色的保存,不会更改游戏流程
            file_data.MarkAsServerSide();
            file_data.SetAsActive();
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
    bool OnHook1(On_FileUtilities.orig_Exists func, string path, bool cloud)
    {
        if (path == null)
        {
            return false;
        }

        return path.StartsWith("SSC@") || func(path, cloud);
    }

    // 原本的path后缀为plr或tplr,根据后缀在特定格式的数据中返回内容
    byte[] OnHook2(On_FileUtilities.orig_ReadAllBytes func, string path, bool cloud)
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

        return func(path, cloud);
    }

    // 只有路径为.SSC结尾的SSC存档才会保存到云端,只需要控制SSC标志和路径后缀即可轻松区分.
    // 路径有三种,PS/[SteamID].plr,Create.SSC,PS/[SteamID].SSC
    void ILHook4(ILContext il)
    {
        var cur = new ILCursor(il);

        // 除了正常的角色存档外,额外放行以.SSC结尾的SSC存档.
        cur.GotoNext(MoveType.After, i => i.MatchCallvirt<PlayerFileData>("get_ServerSideCharacter"));
        cur.Emit(OpCodes.Ldarg_0);
        cur.EmitDelegate<Func<bool, PlayerFileData, bool>>((SSC, file_data) => SSC && !file_data.Path.EndsWith(".SSC"));

        // 获取Array和Tag进行保存上传.
        var array = 0;
        cur.GotoNext(MoveType.After,
            i => i.MatchCallvirt<MemoryStream>(nameof(MemoryStream.ToArray)), i => i.MatchStloc(out array),
            i => i.MatchLdloc(1), i => i.MatchCall(typeof(PlayerLoader), nameof(PlayerLoader.PostSavePlayer)),
            i => i.MatchLdloc(1), i => i.MatchCall("Terraria.ModLoader.IO.PlayerIO", "SaveData")
        );
        cur.Emit(OpCodes.Ldarg_0); // file_data
        cur.Emit(OpCodes.Ldloc_S, (byte)array); // data
        cur.EmitDelegate<Func<TagCompound, PlayerFileData, byte[], TagCompound>>((root, file_data, data) =>
        {
            // 如果存档为符合条件的SSC存档,则上传保存. 上面的拦截不会考虑原版硬核死亡为幽灵的情况,这里还需要额外判断下.
            if (file_data.ServerSideCharacter && file_data.Path.EndsWith(".SSC") && !file_data.Player.ghost)
            {
                var threadStart = new ThreadStart(delegate
                {
                    var mp = Mod.GetPacket();
                    mp.Write((byte)MessageID.SaveSSC);
                    mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
                    mp.Write(file_data.Player.name);
                    mp.Write(data.Length);
                    mp.Write(data);
                    TagIO.Write(root, mp);
                    mp.Write(file_data.Path == "Create.SSC");
                    MessageManager.SendMessage(mp);
                });
                var thread = new Thread(threadStart);
                thread.Start(); //多线程启动匿名方法
            }

            return root;
        });

        // 因为额外放行了特殊的SSC存档,如果是SSC存档,则跳过后续的本地文件保存.
        cur.Emit(OpCodes.Ldarg_0);
        cur.EmitDelegate<Func<PlayerFileData, bool>>(file_data => file_data.ServerSideCharacter);
        var br0 = cur.DefineLabel();
        var ret = cur.DefineLabel();
        cur.Emit(OpCodes.Brfalse_S, br0);
        cur.Emit(OpCodes.Leave_S, ret);
        cur.MarkLabel(br0);
        cur.GotoNext(MoveType.Before, i => i.MatchRet());
        cur.MarkLabel(ret);
    }

    // 硬核死亡删除云存档
    void OnHook3(On_Player.orig_KillMeForGood func, Player player)
    {
        var file_data = Main.ActivePlayerFileData;
        if (file_data.ServerSideCharacter)
        {
            var mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)MessageID.EraseSSC);
            mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
            mp.Write(file_data.Player.name);
            MessageManager.SendMessage(mp);
        }

        func(player);
    }
}