using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;
using Terraria.Utilities;

namespace SSC.Core;

public class ServerViewer : UIState
{
    internal UIPanel Container;
    internal UIGrid CharacterGrid;
    internal Player Dummy;
    internal UICharacterCreation CharacterCreation;
    internal UIPanel CharacterCreationPanel;
    internal UICharacterNameButton NameButton;
    internal UISearchBar NameSearchBar;
    internal UITextPanel<LocalizedText> CreateButton;

    public override void OnActivate()
    {
        Container = new UIKit.SuperPanel
        {
            Width = new StyleDimension(370, 0),
            Height = new StyleDimension(600, 0),
            HAlign = 0.5f, VAlign = 0.5f,
            PaddingRight = 10,
            BackgroundColor = new Color(33, 43, 79) * 0.8f
        };
        Append(Container);

        var bar = new UIScrollbar
        {
            Height = new StyleDimension(-10, 1),
            HAlign = 1, VAlign = 0.5f
        };
        Container.Append(bar);

        Container.Append(CharacterGrid = new UIGrid
        {
            Width = new StyleDimension(-25, 1),
            Height = new StyleDimension(0, 1)
        });
        CharacterGrid.SetScrollbar(bar);

        Dummy = new Player();

        CharacterCreationPanel = new UIPanel
        {
            Width = new StyleDimension(0, 1),
            Height = new StyleDimension(180, 0)
        };
        CharacterCreationPanel.SetPadding(10);
        CharacterGrid.Append(CharacterCreationPanel);

        NameButton = new UICharacterNameButton(Language.GetText("UI.PlayerNameSlot"), LocalizedText.Empty)
        {
            Width = new StyleDimension(0, 1),
            Height = new StyleDimension(40, 0)
        };
        NameButton.OnUpdate += _ =>
        {
            if (!Main.mouseLeft)
            {
                return;
            }

            switch (NameButton.IsMouseHovering)
            {
                case true when !NameSearchBar.IsWritingText:
                case false when NameSearchBar.IsWritingText:
                {
                    NameSearchBar.ToggleTakingText();
                    break;
                }
            }
        };
        CharacterCreationPanel.Append(NameButton);

        NameSearchBar = new UISearchBar(LocalizedText.Empty, 1)
        {
            Width = new StyleDimension(-50, 1),
            Height = new StyleDimension(40, 0),
            Left = new StyleDimension(50, 0)
        };
        NameSearchBar.OnMouseOver += (evt, _) => NameButton.MouseOver(evt);
        NameSearchBar.OnMouseOut += (evt, _) => NameButton.MouseOut(evt);
        NameSearchBar.OnContentsChanged += name => Dummy.name = name;
        CharacterCreationPanel.Append(NameSearchBar);

        CharacterCreationPanel.Append(
            new UIDifficultyButton(Dummy, Lang.menu[26], null, PlayerDifficultyID.SoftCore, Color.Cyan)
            {
                Width = new StyleDimension(-5, 0.5f),
                Height = new StyleDimension(26, 0),
                Top = new StyleDimension(50, 0)
            });
        CharacterCreationPanel.Append(
            new UIDifficultyButton(Dummy, Lang.menu[25], null, PlayerDifficultyID.MediumCore, Main.mcColor)
            {
                Width = new StyleDimension(-5, 0.5f),
                Height = new StyleDimension(26, 0),
                Top = new StyleDimension(50, 0),
                Left = new StyleDimension(5, 0.5f)
            });
        CharacterCreationPanel.Append(
            new UIDifficultyButton(Dummy, Lang.menu[24], null, PlayerDifficultyID.Hardcore, Main.hcColor)
            {
                Width = new StyleDimension(-5, 0.5f),
                Height = new StyleDimension(26, 0),
                Top = new StyleDimension(80, 0)
            });
        CharacterCreationPanel.Append(new UIDifficultyButton(Dummy, Language.GetText("UI.Creative"), null,
            PlayerDifficultyID.Creative, Main.creativeModeColor)
        {
            Width = new StyleDimension(-5, 0.5f),
            Height = new StyleDimension(26, 0),
            Top = new StyleDimension(80, 0),
            Left = new StyleDimension(5, 0.5f)
        });

        CreateButton = new UITextPanel<LocalizedText>(Language.GetText("UI.Create"), 0.7f, true)
        {
            Width = new StyleDimension(0, 1),
            Height = new StyleDimension(30, 0),
            Top = new StyleDimension(115, 0),
            HAlign = 0.5f
        };
        CreateButton.OnMouseOver += (_, _) =>
        {
            CreateButton.BackgroundColor = new Color(73, 94, 171);
            CreateButton.BorderColor = Colors.FancyUIFatButtonMouseOver;
        };
        CreateButton.OnMouseOut += (_, _) =>
        {
            CreateButton.BackgroundColor = new Color(63, 82, 151) * 0.8f;
            CreateButton.BorderColor = Color.Black;
        };
        CreateButton.OnLeftClick += (_, _) =>
        {
            var Character = new Player();
            CharacterCreation = new UICharacterCreation(Character); // 注意会重置Player的难度

            Character.name = Dummy.name;
            Character.difficulty = Dummy.difficulty;
            var invoke_name = "SetupPlayerStatsAndInventoryBasedOnDifficulty";
            var invoke = typeof(UICharacterCreation).GetMethod(invoke_name, (BindingFlags)36);
            invoke?.Invoke(CharacterCreation, []);

            // from Player.CopyVisuals
            Character.skinVariant = HookManager.JoinPlayer.skinVariant;
            Character.skinColor = HookManager.JoinPlayer.skinColor;
            Character.eyeColor = HookManager.JoinPlayer.eyeColor;
            Character.hair = HookManager.JoinPlayer.hair;
            Character.hairColor = HookManager.JoinPlayer.hairColor;
            Character.shirtColor = HookManager.JoinPlayer.shirtColor;
            Character.underShirtColor = HookManager.JoinPlayer.underShirtColor;
            Character.pantsColor = HookManager.JoinPlayer.pantsColor;
            Character.shoeColor = HookManager.JoinPlayer.shoeColor;

            var data = new PlayerFileData("Create.SSC", false)
            {
                Metadata = FileMetadata.FromCurrentSettings(FileType.Player), Player = Character
            };
            data.MarkAsServerSide();

            var SavePlayer = typeof(Player).GetMethod("InternalSavePlayerFile", (BindingFlags)40);
            FileUtilities.ProtectedInvoke(() => SavePlayer?.Invoke(null, [data]));

            NameSearchBar.SetContents("");
            Dummy.difficulty = PlayerDifficultyID.SoftCore;
        };
        CharacterCreationPanel.Append(CreateButton);
    }

    public static string DifficultyTextValue(byte difficulty)
    {
        return Language.GetTextValue(difficulty switch
        {
            0 => "UI.Softcore",
            1 => "UI.Mediumcore",
            2 => "UI.Hardcore",
            3 => "UI.Creative",
            _ => "Unknown"
        });
    }

    public static Color DifficultyTextColor(byte difficulty)
    {
        return difficulty switch
        {
            1 => Main.mcColor,
            2 => Main.hcColor,
            3 => Main.creativeModeColor,
            _ => Color.White
        };
    }

    public void Calc(TagCompound obj)
    {
        CharacterGrid.Clear();
        foreach (var tag in obj.Get<List<TagCompound>>(SSC.GetPID()))
        {
            var item = new UIPanel
            {
                Width = new StyleDimension(0, 1),
                Height = new StyleDimension(80, 0),
                PaddingBottom = 10
            };
            item.OnMouseOver += (_, _) =>
            {
                item.BackgroundColor = new Color(73, 94, 171);
                item.BorderColor = new Color(89, 116, 213);
            };
            item.OnMouseOut += (_, _) =>
            {
                item.BackgroundColor = new Color(63, 82, 151) * 0.7f;
                item.BorderColor = new Color(89, 116, 213) * 0.7f;
            };
            CharacterGrid.Add(item);

            item.Append(new UIText(tag.GetString("name"))
            {
                Height = new StyleDimension(30, 0),
                TextColor = DifficultyTextColor(tag.GetByte("game_mode"))
            });

            item.Append(new UIText(DifficultyTextValue(tag.GetByte("game_mode")))
            {
                Height = new StyleDimension(30, 0),
                HAlign = 1,
                TextColor = DifficultyTextColor(tag.GetByte("game_mode"))
            });

            item.Append(new UIImage(Main.Assets.Request<Texture2D>("Images/UI/Divider"))
            {
                Width = new StyleDimension(0, 1),
                HAlign = 0.5f, VAlign = 0.5f,
                ScaleToFit = true
            });

            item.Append(new UIText(new TimeSpan(tag.GetLong("play_time")).ToString(@"dd\:hh\:mm\:ss"))
            {
                Height = new StyleDimension(15, 0),
                HAlign = 0.5f, VAlign = 1,
                TextColor = DifficultyTextColor(tag.GetByte("game_mode"))
            });

            var itemPlayButton = new UIImageButton(Main.Assets.Request<Texture2D>("Images/UI/ButtonPlay"))
            {
                VAlign = 1
            };
            itemPlayButton.OnLeftClick += (_, _) =>
            {
                var mp = ModContent.GetInstance<SSC>().GetPacket();
                mp.Write((byte)MessageID.GoGoSSC);
                mp.Write(SSC.GetPID());
                mp.Write(tag.GetString("name"));
                mp.Send();
            };
            itemPlayButton.OnUpdate += _ =>
            {
                if (itemPlayButton.IsMouseHovering)
                {
                    Main.instance.MouseText(Language.GetTextValue("UI.Play"));
                }
            };
            item.Append(itemPlayButton);

            var itemDeleteButton = new UIImageButton(Main.Assets.Request<Texture2D>("Images/UI/ButtonDelete"))
            {
                HAlign = 1, VAlign = 1
            };
            itemDeleteButton.OnRightDoubleClick += (_, _) =>
            {
                var mp = ModContent.GetInstance<SSC>().GetPacket();
                mp.Write((byte)MessageID.EraseSSC);
                mp.Write(SSC.GetPID());
                mp.Write(tag.Get<string>("name"));
                mp.Send();
            };
            itemDeleteButton.OnUpdate += _ =>
            {
                if (itemDeleteButton.IsMouseHovering)
                {
                    Main.instance.MouseText(
                        $"{Language.GetTextValue("UI.Delete")} ({Language.GetTextValue("Mods.SSC.RightDoubleClick")})");
                }
            };
            item.Append(itemDeleteButton);
        }

        CharacterGrid.Add(CharacterCreationPanel);
    }
}