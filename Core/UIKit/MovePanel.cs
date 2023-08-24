using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace SSC.Core.UIKit;

public class MovePanel : UIPanel
{
    public bool Moving;
    public Vector2 MoveVector;

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        Moving = true;
        MoveVector = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);

        Moving = false;
        Left.Set(evt.MousePosition.X - MoveVector.X, 0);
        Top.Set(evt.MousePosition.Y - MoveVector.Y, 0);
        Recalculate();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (ContainsPoint(Main.MouseScreen))
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        if (Moving)
        {
            Left.Set(Main.mouseX - MoveVector.X, 0f); // Main.MouseScreen.X and Main.mouseX are the same
            Top.Set(Main.mouseY - MoveVector.Y, 0f);
            Recalculate();
        }

        var space = Parent.GetViewCullingArea();
        var area = GetViewCullingArea();
        if (!space.Contains(area))
        {
            if (area.X < space.X)
            {
                Left.Pixels += space.X - area.X;
            }

            if (area.X + area.Width > space.Width)
            {
                Left.Pixels -= area.X + area.Width - space.Width;
            }

            if (area.Y < space.Y)
            {
                Top.Pixels += space.Y - area.Y;
            }

            if (area.Y + area.Height > space.Height)
            {
                Top.Pixels -= area.Y + area.Height - space.Height;
            }

            Recalculate();
        }
    }
}