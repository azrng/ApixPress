using Avalonia.Controls;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;

namespace ApixPress.App.Services.Implementations;

public sealed class WindowHostService : IWindowHostService, ISingletonDependency
{
    public Window? MainWindow { get; set; }
}
