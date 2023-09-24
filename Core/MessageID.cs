namespace SSC.Core;

public enum MessageID : byte
{
    MessageSegment, // 用来分段包含其他Message内容

    SaveSSC, // 客户端->服务端,id,name,data,root,first

    GoGoSSC, // 客户端->服务端,id,name.服务端->客户端,data,root.

    EraseSSC, // 客户端->服务端,id,name

    ForceSaveSSC, // 服务端->客户端
}