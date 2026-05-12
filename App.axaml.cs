using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OllamaManager.Services;
using OllamaManager.ViewModels;
using OllamaManager.Views;

namespace OllamaManager;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainViewModel? _vm;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _vm = new MainViewModel();
            _mainWindow = new MainWindow { DataContext = _vm };
            desktop.MainWindow = _mainWindow;

            _mainWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                _mainWindow.Hide();
            };

            _mainWindow.Show();

            var icons = TrayIcon.GetIcons(this);
            if (icons is { Count: > 0 })
            {
                var tray = icons[0];
                tray.Clicked += (_, _) => ToggleWindow();

                if (tray.Menu?.Items.Count >= 3)
                {
                    ((NativeMenuItem)tray.Menu.Items[0]).Click += (_, _) => ToggleWindow();
                    ((NativeMenuItem)tray.Menu.Items[2]).Click += (_, _) => Quit();
                }
            }
        }

        base.OnFrameworkInitializationCompleted();

        // Solo per dotnet run (fuori dal bundle): il bundle usa già CFBundleIconFile in Info.plist
        var infoPlist = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "Info.plist");
        if (!System.IO.File.Exists(infoPlist))
            MacOSHelper.SetDockIcon();
    }

    private void ToggleWindow()
    {
        if (_mainWindow == null) return;
        if (_mainWindow.IsVisible)
            _mainWindow.Hide();
        else
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void Quit()
    {
        _vm?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
