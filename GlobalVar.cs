namespace SSC;

internal static class GlobalVar
{
    internal static bool SSCisRunning;

    static GlobalVar()
    {
        Init();
    }

    static void Init()
    {
        SSCisRunning = false;
    }
}