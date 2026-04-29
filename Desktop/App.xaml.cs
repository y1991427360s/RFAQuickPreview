namespace RFAQuickPreview.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;
        DispatcherUnhandledException += (_, args) =>
        {
            Services.AppLog.Write("DispatcherUnhandledException: " + args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Services.AppLog.Write("UnhandledException: " + args.ExceptionObject);
        };

        var initialFolder = e.Args.Length > 0 ? e.Args[0] : null;
        Services.AppLog.Write("Startup initialFolder=" + initialFolder);
        var window = new MainWindow(initialFolder);
        MainWindow = window;
        window.Show();
    }
}
