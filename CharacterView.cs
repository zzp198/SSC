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

namespace SSC;

public class CharacterView : UIState
{
    internal UIContainer Container;
    internal UIGrid CharacterGrid;
    internal Player Character;
    internal UICharacterCreation CharacterCreation;
    internal UIPanel CharacterCreationPanel;
    internal UICharacterNameButton NameButton;
    internal UISearchBar NameSearchBar;
    internal UITextPanel<LocalizedText> CreateButton;

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

    public override void OnActivate()
    {
        Append(Container = new UIContainer
        {
            Width = new StyleDimension(370, 0),
            Height = new StyleDimension(600, 0),
            HAlign = 0.5f, VAlign = 0.5f,
            PaddingRight = 10,
            BackgroundColor = new Color(33, 43, 79) * 0.8f
        });

        var scrollbar = new UIScrollbar
        {
            Height = new StyleDimension(-10, 1),
            HAlign = 1, VAlign = 0.5f
        };
        Container.Append(scrollbar);

        Container.Append(CharacterGrid = new UIGrid
        {
            Width = new StyleDimension(-25, 1),
            Height = new StyleDimension(0, 1)
        });
        CharacterGrid.SetScrollbar(scrollbar);

        CharacterCreation = new UICharacterCreation(Character = new Player());

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
        NameSearchBar.OnContentsChanged += name => Character.name = name;
        CharacterCreationPanel.Append(NameSearchBar);

        CharacterCreationPanel.Append(new UIDifficultyButton(Character, Lang.menu[26], null, PlayerDifficultyID.SoftCore, Color.Cyan)
        {
            Width = new StyleDimension(-5, 0.5f),
            Height = new StyleDimension(26, 0),
            Top = new StyleDimension(50, 0)
        });
        CharacterCreationPanel.Append(new UIDifficultyButton(Character, Lang.menu[25], null, PlayerDifficultyID.MediumCore, Main.mcColor)
        {
            Width = new StyleDimension(-5, 0.5f),
            Height = new StyleDimension(26, 0),
            Top = new StyleDimension(50, 0),
            Left = new StyleDimension(5, 0.5f)
        });
        CharacterCreationPanel.Append(new UIDifficultyButton(Character, Lang.menu[24], null, PlayerDifficultyID.Hardcore, Main.hcColor)
        {
            Width = new StyleDimension(-5, 0.5f),
            Height = new StyleDimension(26, 0),
            Top = new StyleDimension(80, 0)
        });
        CharacterCreationPanel.Append(
            new UIDifficultyButton(Character, Language.GetText("UI.Creative"), null, PlayerDifficultyID.Creative, Main.creativeModeColor)
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
            var invoke = typeof(UICharacterCreation).GetMethod("SetupPlayerStatsAndInventoryBasedOnDifficulty", (BindingFlags)36);
            invoke?.Invoke(CharacterCreation, Array.Empty<object>());
            Character.statLife = Character.statLifeMax = ModContent.GetInstance<ServerConfig>().StartLife;
            Character.statMana = Character.statManaMax = ModContent.GetInstance<ServerConfig>().StartMana;
            var data = new PlayerFileData("Create.SSC", false)
            {
                Metadata = FileMetadata.FromCurrentSettings(FileType.Player), Player = Character
            };
            data.MarkAsServerSide();

            var SavePlayer = typeof(Player).GetMethod("InternalSavePlayerFile", BindingFlags.NonPublic | BindingFlags.Static);
            FileUtilities.ProtectedInvoke(() => SavePlayer?.Invoke(null, new object[] { data }));

            NameSearchBar.SetContents("");
            Character.difficulty = PlayerDifficultyID.SoftCore;
        };
        CharacterCreationPanel.Append(CreateButton);
    }

    public void Calc(TagCompound obj)
    {
        CharacterGrid.Clear();
        foreach (var tag in obj.Get<List<TagCompound>>(SteamUser.GetSteamID().m_SteamID.ToString()))
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
                mp.Write((byte)SSC.MsgID.TryLoad);
                mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
                mp.Write(tag.GetString("name"));
                SSC.StreamPatcher(mp);
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
                mp.Write((byte)SSC.MsgID.TryRemove);
                mp.Write(SteamUser.GetSteamID().m_SteamID.ToString());
                mp.Write(tag.Get<string>("name"));
                SSC.StreamPatcher(mp);
            };
            itemDeleteButton.OnUpdate += _ =>
            {
                if (itemDeleteButton.IsMouseHovering)
                {
                    Main.instance.MouseText($"{Language.GetTextValue("UI.Delete")} (Right-Double-Click/右键双击)");
                }
            };
            item.Append(itemDeleteButton);
        }

        CharacterGrid.Add(CharacterCreationPanel);
    }
}