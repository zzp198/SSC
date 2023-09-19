using System.Collections.Generic;
using System.IO;
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
    internal uint SaveCountdown;

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

        var users = new DirectoryInfo(Path.Combine(SSC.PATH, SSC.MapID)).GetDirectories(); // 获取一级子目录
        foreach (var user in users)
        {
            root.Set(user.Name, new List<TagCompound>());
            foreach (var file_info in user.GetFiles("*.plr")) // 获取一级子文件
            {
                var file_data = Player.LoadPlayer(file_info.FullName, false);
                root.Get<List<TagCompound>>(user.Name).Add(new TagCompound
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
        // TODO
        // (UI?.CurrentState as ServerViewer)?.Calc(root);
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient || MarkSystem.AnyActiveDanger)
        {
            return;
        }

        SaveCountdown++;
        if (SaveCountdown > 3600)
        {
            SaveCountdown = 0;
            if (Main.ActivePlayerFileData.Path.EndsWith(".SSC"))
            {
                Player.SavePlayer(Main.ActivePlayerFileData);
            }
        }
    }

    public override void Unload()
    {
        UI = null;
    }
}