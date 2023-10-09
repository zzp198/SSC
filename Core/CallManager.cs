using System.IO;
using Steamworks;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;

namespace SSC.Core;

public class CallManager : ModSystem
{
    public void Anonymous(object name, byte difficulty)
    {
        var fileData = new PlayerFileData(Path.Combine(Main.PlayerPath, $"{name}.plr"), false)
        {
            Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
            Player = new Player
            {
                name = SteamUser.GetSteamID().m_SteamID.ToString(), difficulty = difficulty,
                // MessageID -> StatLife:16  Ghost:13  Dead:12&16
                statLife = 0, statMana = 0, dead = true, ghost = true,
                // 避免因为进入世界的自动复活,导致客户端与服务端失去同步
                respawnTimer = int.MaxValue, lastTimePlayerWasSaved = long.MaxValue,
                savedPerPlayerFieldsThatArentInThePlayerClass = new Player.SavedPlayerDataWithAnnoyingRules()
            }
        };
        // 正常情况下不会拥有此标记,区别与Main.SSC,这个只会影响本地角色的保存,不会更改游戏流程
        fileData.MarkAsServerSide();
        fileData.SetAsActive();
    }
}