// using System.ComponentModel;
// using Terraria.ModLoader.Config;
//
// namespace SSC.Core.Configs;
//
// public class ServerConfig : ModConfig
// {
//     public override ConfigScope Mode => ConfigScope.ServerSide;
//
//     [Slider, Range(1, 500)]
//     [ColorHSLSlider, SliderColor(200, 0, 0)] //
//     [DefaultValue(100)]
//     public int StartLife = 100;
//
//     [Slider, Range(0, 200)]
//     [ColorHSLSlider, SliderColor(0, 0, 200)] //
//     [DefaultValue(20)]
//     public int StartMana = 20;
//
//
//     [DefaultValue(false)] //
//     public bool EveryWorld = false;
// }