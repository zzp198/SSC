// using Microsoft.Xna.Framework;
// using Terraria;
// using Terraria.GameContent.UI.Elements;
// using Terraria.UI;
//
// namespace SSC.Core;
//
// public class UIMovePanel : UIPanel
// {
//     public bool Moving;
//     public Vector2 MoveVector;
//
//     public override void LeftMouseDown(UIMouseEvent evt)
//     {
//         Moving = true;
//         MoveVector = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
//     }
//
//     public override void LeftMouseUp(UIMouseEvent evt)
//     {
//         Moving = false;
//     }
//
//     public override void Update(GameTime gameTime)
//     {
//         if (ContainsPoint(Main.MouseScreen))
//         {
//             Main.LocalPlayer.mouseInterface = true;
//         }
//
//         var space = Parent.GetViewCullingArea();
//         var area = GetViewCullingArea();
//         if (!space.Contains(area))
//         {
//             if (area.X < space.X)
//             {
//                 Left.Pixels += space.X - area.X;
//             }
//
//             if (area.X + area.Width > space.Width)
//             {
//                 Left.Pixels -= area.X + area.Width - space.Width;
//             }
//
//             if (area.Y < space.Y)
//             {
//                 Top.Pixels += space.Y - area.Y;
//             }
//
//             if (area.Y + area.Height > space.Height)
//             {
//                 Top.Pixels -= area.Y + area.Height - space.Height;
//             }
//         }
//         else if (Moving)
//         {
//             Left.Set(Main.mouseX - MoveVector.X, 0f);
//             Top.Set(Main.mouseY - MoveVector.Y, 0f);
//         }
//
//         base.Update(gameTime); // don't remove.
//     }
// }