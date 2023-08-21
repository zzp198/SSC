using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace SSC.Core;

public class SSC : Mod
{
    public static string PATH => Path.Combine(Main.SavePath, nameof(SSC));
    public static string WorldID => Main.ActiveWorldFileData.UniqueId.ToString();

    public override void Load()
    {
        // 白嫖一只小兔子
        Main.runningCollectorsEdition = true;
    }
}