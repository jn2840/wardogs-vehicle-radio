namespace WardogsBridge;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--self-test")
        {
            SelfTest.Run(args[1]);
            return;
        }
        Application.Run(new BridgeForm());
    }
}
