using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace SSC.Core;

public class ViewState : UIState
{
    public UIPanel BasePanel;
    public bool Moving;
    public Vector2 MoveVector;


    public override void OnActivate()
    {
        BasePanel = new UIKit.MovePanel()
        {
            Width = new StyleDimension(370, 0),
            Height = new StyleDimension(600, 0),
            HAlign = 0.5f, VAlign = 0.5f,
            PaddingRight = 10,
            BackgroundColor = new Color(33, 43, 79) * 0.8f
        };

        Append(BasePanel);
    }
}