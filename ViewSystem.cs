using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace SSC;

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
}