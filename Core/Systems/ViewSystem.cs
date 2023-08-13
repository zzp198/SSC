using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace SSC.Core.Systems;

public class ViewSystem : ModSystem
{
    internal static UserInterface View;

    public override void Load()
    {
        if (!Main.dedServ)
        {
            View = new UserInterface();
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        var index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index != -1)
        {
            layers.Insert(index, new LegacyGameInterfaceLayer("Vanilla: SSC", () =>
            {
                if (View?.CurrentState != null)
                {
                    View.Draw(Main.spriteBatch, Main.gameTimeCache);
                }

                return true;
            }, InterfaceScaleType.UI));
        }
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (View?.CurrentState != null)
        {
            View.Update(gameTime);
        }
    }

    public override void Unload()
    {
        View = null;
    }

    public override void NetSend(BinaryWriter writer)
    {
        var tag = new TagCompound();
        var dir = SSC.PATH;
        // if (ModContent.GetInstance<Configs.ServerConfig>().EveryWorld)
        // {
        //     dir = Path.Combine(dir, Main.ActiveWorldFileData.UniqueId.ToString());
        // }

        Utils.TryCreatingDirectory(dir);

        var dirs = new DirectoryInfo(dir).GetDirectories();
        foreach (var dir_info in dirs)
        {
            tag.Set(dir_info.Name, new List<TagCompound>());
            foreach (var file_info in dir_info.GetFiles("*.plr"))
            {
                var file_data = Player.LoadPlayer(file_info.FullName, false);
                tag.Get<List<TagCompound>>(dir_info.Name).Add(new TagCompound
                {
                    { "name", file_data.Player.name },
                    { "game_mode", file_data.Player.difficulty },
                    { "play_time", file_data.GetPlayTime().Ticks },
                });
            }
        }

        TagIO.Write(tag, writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        var tag = TagIO.Read(reader);
        if (ViewSystem.View.CurrentState is CharacterView)
        {
            ((CharacterView)ViewSystem.View.CurrentState).Calc(tag);
        }
    }
}