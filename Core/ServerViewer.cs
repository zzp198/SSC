using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.States;
using Terraria.UI;

namespace SSC.Core;

public class ServerViewer : UIState
{
    public override void OnActivate()
    {
        var CharacterCreation = new UICharacterCreation(new Player());

        var playerUI = ((List<UIElement>)CharacterCreation.Children)[0];

        
        
        var Container = new UIKit.MovePanel
        {
            Width = new StyleDimension(playerUI.GetDimensions().Width, 0),
            Height = new StyleDimension(playerUI.GetDimensions().Width, 0),
            HAlign = 0.5f, VAlign = 0.5f,
            // PaddingRight = 10,
            BackgroundColor = new Color(33, 43, 79) * 0.8f,
            OverflowHidden = true

        };
        
        Append(Container);


        Container.Append(CharacterCreation);
    }
}