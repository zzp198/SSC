using System;
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

namespace SSC.Core.Systems;

public class HookSystem : ModSystem
{
    // 时隔几月,我几乎忘记了我Hook的位置和实现思路了......
    public override void Load()
    {
        // IL.Terraria.NetMessage.SendData 1.4.4 changed
        IL_NetMessage.SendData += IgnoreGameMode;
        IL_MessageBuffer.GetData += SSCInit;
        IL_Main.DrawInterface += HideLayerBySSC;
        IL_Main.DoUpdate_AutoSave += FixAutoSaveTime;
        On_FileUtilities.Exists += ExistByPass;
        On_FileUtilities.ReadAllBytes += ReadByPass;
        On_Player.InternalSavePlayerFile += SSCSave;
        On_Player.KillMeForGood += GoodKill;
    }

    // 在客户端进行多人模式连接时,服务端会额外发送自己的GameMode给客户端
    void IgnoreGameMode(ILContext il)
    {
        // case 3:
        //    writer.Write((byte) remoteClient);
        //    writer.Write(false);
        // -> writer.Write((byte) Main.GameMode)
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

    // 客户端处理额外的GameMode,以此初始化所有配置,并以无毒无公害的永久幽魂形式进入世界
    void SSCInit(ILContext il)
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
            var file_data = new PlayerFileData(Path.Combine(Main.PlayerPath, $"{SteamUser.GetSteamID().m_SteamID}.plr"), false)
            {
                Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
                Player = new Player
                {
                    name = SteamUser.GetSteamID().m_SteamID.ToString(), difficulty = game_mode,
                    // MsgID -> StatLife:16  Ghost:13  Dead:12&16
                    statLife = 0, statMana = 0, dead = true, ghost = true,
                    // 避免因为进入世界的自动复活,导致客户端与服务端失去同步
                    respawnTimer = int.MaxValue, lastTimePlayerWasSaved = long.MaxValue,
                    savedPerPlayerFieldsThatArentInThePlayerClass = new Player.SavedPlayerDataWithAnnoyingRules()
                }
            };
            // 正常情况下不会拥有此标记,区别与Main.SSC,这个只会影响本地角色的保存,不会更改游戏流程
            file_data.MarkAsServerSide();
            file_data.SetAsActive();
            ViewSystem.View.SetState(new CharacterView()); // 唯一设置界面的地方
        });
    }

    // SSC界面下禁止其他MOD的界面和操作,避免影响服务端同步和条件判断
    void HideLayerBySSC(ILContext il)
    {
        var cur = new ILCursor(il);
        cur.GotoNext(MoveType.After, i => i.MatchCall(typeof(SystemLoader), nameof(SystemLoader.ModifyInterfaceLayers)));
        cur.EmitDelegate<Func<List<GameInterfaceLayer>, List<GameInterfaceLayer>>>(layers =>
        {
            if (ViewSystem.View.CurrentState != null)
            {
                layers.ForEach(layer => layer.Active = layer.Name switch
                {
                    "Vanilla: Map / Minimap" => false,
                    "Vanilla: Resource Bars" => false,
                    _ => layer.Name.StartsWith("Vanilla")
                });
            }

            return layers;
        });
    }

    // 缩短自动保存的间隔为60秒
    void FixAutoSaveTime(ILContext il)
    {
        var cur = new ILCursor(il);
        cur.GotoNext(MoveType.After, i => i.MatchLdcI4(300000));
        cur.EmitDelegate<Func<long, long>>(_ => 60000);
    }

    // 放行特定的数据格式
    bool ExistByPass(On_FileUtilities.orig_Exists func, string path, bool cloud)
    {
        return path.StartsWith("SSC@") || func(path, cloud);
    }

    // 原本的path后缀为plr或tplr,根据后缀在特定格式的数据中返回内容
    byte[] ReadByPass(On_FileUtilities.orig_ReadAllBytes func, string path, bool cloud)
    {
        if (path.StartsWith("SSC@"))
        {
            var regex = new Regex(@"^SSC@(?<plr>[0-9A-F]+)@(?<tplr>[0-9A-F]+)@\.(?<type>plr|tplr)$");
            var match = regex.Match(path);
            if (match.Success)
            {
                return Convert.FromHexString(match.Groups[match.Groups["type"].Value].Value);
            }

            throw new ArgumentException("Regex match failed.");
        }

        return func(path, cloud);
    }

    // 目前改版后没有办法同时获取到data和tag进行同步上传,只能重写保存函数,但是不具备向后兼容性,各个版本可能会有变动导致mod很容易过期(悲
    void SSCSave(On_Player.orig_InternalSavePlayerFile func, PlayerFileData file_data)
    {
        if (!file_data.ServerSideCharacter || file_data.Player.ghost)
        {
            func(file_data); // Ghost的Player会跳转原函数,然后因为SSC而被忽略
            return;
        }

        var rijndaelManaged = new RijndaelManaged();
        using (var memoryStream = new MemoryStream(2000))
        {
            var key = (byte[])typeof(Player).GetField("ENCRYPTION_KEY", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            using (var cryptoStream = new CryptoStream(memoryStream, rijndaelManaged.CreateEncryptor(key, key), CryptoStreamMode.Write))
            {
                var Serialize = typeof(Player).GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Static);
                var PlayerIO = Assembly.Load("tModLoader").GetType("Terraria.ModLoader.IO.PlayerIO");
                var SaveData = PlayerIO?.GetMethod("SaveData", BindingFlags.NonPublic | BindingFlags.Static);
                using (var binaryWriter = new BinaryWriter(cryptoStream))
                {
                    PlayerLoader.PreSavePlayer(file_data.Player);
                    binaryWriter.Write(279);
                    file_data.Metadata.Write(binaryWriter);
                    // 其中包括PlayTime的Ticks
                    Serialize?.Invoke(null, new object[] { file_data, file_data.Player, binaryWriter });
                    binaryWriter.Flush();
                    cryptoStream.FlushFinalBlock();
                    memoryStream.Flush();
                    var data = memoryStream.ToArray();
                    PlayerLoader.PostSavePlayer(file_data.Player);
                    var tag = (TagCompound)SaveData?.Invoke(null, new object[] { file_data.Player });

                    var mp = Mod.GetPacket();
                    mp.Write((byte)SSC.MsgID.TrySave);
                    mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
                    mp.Write(file_data.Player.name);
                    mp.Write(data.Length);
                    mp.Write(data);
                    TagIO.Write(tag!, mp);
                    mp.Write(file_data.Path == "Create.SSC");
                    SSC.StreamPatcher(mp);
                }
            }
        }
    }

    // 硬核死亡删除云存档
    void GoodKill(On_Player.orig_KillMeForGood func, Player player)
    {
        var file_data = Main.ActivePlayerFileData;
        if (file_data.ServerSideCharacter)
        {
            var mp = ModContent.GetInstance<SSC>().GetPacket();
            mp.Write((byte)SSC.MsgID.TryRemove);
            mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
            mp.Write(file_data.Player.name);
            mp.Send();
        }

        func(player);
    }
}