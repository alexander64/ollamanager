using System.Windows.Input;
using ReactiveUI;

namespace OllamaManager.Models;

public class EnvEntry : ReactiveObject
{
    private string _key = string.Empty;
    public string Key
    {
        get => _key;
        set => this.RaiseAndSetIfChanged(ref _key, value);
    }

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public ICommand? RemoveCommand { get; internal set; }
}
