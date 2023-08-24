using Terraria.ModLoader;

namespace SSC.Core;

public class TestPlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        ModContent.GetInstance<ViewSystem>().View?.SetState(new ViewState());
    }
}