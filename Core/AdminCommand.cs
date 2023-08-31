using System;
using Terraria.ModLoader;

namespace SSC.Core;

public class AdminCommand : ModCommand
{
    public override string Command => "admin";
    public override CommandType Type => CommandType.Console;

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green; //设置前景色，即字体颜色
        Console.WriteLine($"SSC Admin Code: {SSC.Password}");
        Console.ResetColor(); //将控制台的前景色和背景色设为默认值
    }
}