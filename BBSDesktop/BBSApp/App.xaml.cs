using Microsoft.UI.Xaml;

namespace BBSApp;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services.RateBookStore.Current.EnsureLoaded();
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
