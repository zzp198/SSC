using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace SSC.Core;

public class SaveCommand : ModCommand
{
    static double LastSave = new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

    public override string Command => "save";
    public override CommandType Type => CommandType.Chat;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds - LastSave > 60)
            {
                LastSave = new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;
                Player.SavePlayer(Main.ActivePlayerFileData);
            }
            else
            {
                ChatHelper.DisplayMessage(NetworkText.FromKey("Mods.SSC.SaveTooFast"), Color.Yellow, byte.MaxValue);
            }
        }
    }
}