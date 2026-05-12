using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using OllamaManager.ViewModels;
using ReactiveUI;

namespace OllamaManager.Views;

public partial class MainWindow : Window
{
    private CompositeDisposable? _subs;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _subs?.Dispose();
        if (DataContext is not MainViewModel vm) return;

        _subs = new CompositeDisposable(
            vm.WhenAnyValue(x => x.OllamaLog)
              .Subscribe(_ => ScrollToEnd(OllamaLogBox)),
            vm.WhenAnyValue(x => x.MlxLog)
              .Subscribe(_ => ScrollToEnd(MlxLogBox)),
            vm.WhenAnyValue(x => x.OpenWebUILog)
              .Subscribe(_ => ScrollToEnd(OpenWebUILogBox))
        );
    }

    private static void ScrollToEnd(TextBox box)
    {
        var len = box.Text?.Length ?? 0;
        if (len > 0) box.CaretIndex = len;
    }
}
