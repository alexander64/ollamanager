using System.Windows.Input;
using ReactiveUI;

namespace OllamaManager.Models;

public class OllamaModel : ReactiveObject
{
    public string Name { get; init; } = "";
    public string Size { get; init; } = "";

    public ICommand? DeleteCommand { get; set; }
}
