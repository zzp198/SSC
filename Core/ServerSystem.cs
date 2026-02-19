using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace SSC.Core;

public class ServerSystem : ModSystem
{
    internal UserInterface UI;
    internal uint Timer;
    internal Task SaveTask;
    public static int Count;

    public override void Load()
    {
        if (!Main.dedServ)
        {
            UI = new UserInterface();
        }
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (UI?.CurrentState != null)
        {
            UI.Update(gameTime);
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        var index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index != -1)
        {
            layers.Insert(index, new LegacyGameInterfaceLayer("Vanilla: SSC", () =>
            {
                if (UI?.CurrentState != null)
                {
                    UI.Draw(Main.spriteBatch, Main.gameTimeCache);
                }

                return true;
            }, InterfaceScaleType.UI));
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        var root = new TagCompound();

        // SSC/ 或者 SSC/XXX-X-XXX/,其中第一级为SteamID文件夹,第二级为PLR文件.
        // 当为SSC/时,会读取地图文件夹,但里面只有SteamID文件夹,没有PLR文件,所以无性能影响.
        Utils.TryCreatingDirectory(Path.Combine(SSC.PATH, SSC.MapID));

        var dirs = new DirectoryInfo(Path.Combine(SSC.PATH, SSC.MapID)).GetDirectories(); // 获取一级子目录
        foreach (var dir in dirs)
        {
            root.Set(dir.Name, new List<TagCompound>());
            foreach (var file_info in dir.GetFiles("*.plr")) // 获取一级子文件
            {
                var file_data = Player.LoadPlayer(file_info.FullName, false);
                root.Get<List<TagCompound>>(dir.Name).Add(new TagCompound
                {
                    { "name", file_data.Player.name },
                    { "game_mode", file_data.Player.difficulty },
                    { "play_time", file_data.GetPlayTime().Ticks },
                });
            }
        }

        TagIO.Write(root, writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        var root = TagIO.Read(reader);
        (UI?.CurrentState as ServerViewer)?.Calc(root);
    }

    MethodInfo InternalSave = typeof(Player).GetMethod("InternalSavePlayerFile", (BindingFlags)40);

    public override void PostUpdateEverything()
    {
        if (Main.LocalPlayer.ghost)
        {
            Count++;
            if (Count > 60)
            {
                Count = 0;

                int floorX = Main.spawnTileX;
                int floorY = Main.spawnTileY;
                Main.LocalPlayer.Spawn_GetPositionAtWorldSpawn(ref floorX, ref floorY);

                var spawnPosition = new Vector2(floorX * 16, floorY * 16);
                if (Vector2.Distance(Main.LocalPlayer.position, spawnPosition) > 50)
                {
                    Main.LocalPlayer.position = spawnPosition;
                }
            }
        }

        var serverConfig = ModContent.GetInstance<ServerConfig>();

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (serverConfig.DontSaveWhenBossFight && MarkSystem.AnyActiveDanger)
        {
            return;
        }

        Timer++;
        if (Timer > serverConfig.AutoSave * 60)
        {
            Timer = 0;
            if (SaveTask is not { Status: TaskStatus.Running })
            {
                SaveTask = Task.Run(() => { InternalSave.Invoke(null, new object[] { Main.ActivePlayerFileData }); });
            }
        }
    }

    public override void PreSaveAndQuit()
    {
        base.PreSaveAndQuit();
        // 在游戏退出前保存一次
        InternalSave.Invoke(null, new object[] { Main.ActivePlayerFileData });
    }

    public override void Unload()
    {
        UI = null;
    }
}