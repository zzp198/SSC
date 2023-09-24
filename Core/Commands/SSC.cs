using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SSC.Core.Commands;

public class SSC : ModCommand
{
    public override string Command => "SSC";
    public override CommandType Type => CommandType.Chat | CommandType.Console;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        switch (Main.netMode)
        {
            case NetmodeID.MultiplayerClient:
            {
                Player.SavePlayer(Main.ActivePlayerFileData);
                break;
            }
            case NetmodeID.Server:
            {
                var mp = Mod.GetPacket();
                mp.Write((byte)MessageID.ForceSaveSSC);
                MessageManager.SendMessage(mp);
                break;
            }
        }
    }
}