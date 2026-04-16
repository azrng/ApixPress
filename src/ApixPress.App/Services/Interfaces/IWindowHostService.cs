using Avalonia.Controls;

namespace ApixPress.App.Services.Interfaces;

public interface IWindowHostService
{
    Window? MainWindow { get; set; }
}
