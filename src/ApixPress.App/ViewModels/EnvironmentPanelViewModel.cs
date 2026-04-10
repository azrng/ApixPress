using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class EnvironmentPanelViewModel : ViewModelBase
{
    private readonly IEnvironmentVariableService _environmentVariableService;

    public EnvironmentPanelViewModel(IEnvironmentVariableService environmentVariableService)
    {
        _environmentVariableService = environmentVariableService;
    }

    public ObservableCollection<EnvironmentVariableItemViewModel> EnvironmentVariables { get; } = [];

    [ObservableProperty]
    private string activeEnvironmentName = "Default";

    public async Task LoadVariablesAsync()
    {
        EnvironmentVariables.Clear();
        var items = await _environmentVariableService.GetVariablesAsync(ActiveEnvironmentName, CancellationToken.None);
        foreach (var item in items)
        {
            EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
            {
                Id = item.Id,
                EnvironmentName = item.EnvironmentName,
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            });
        }

        if (EnvironmentVariables.Count == 0)
        {
            EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
            {
                EnvironmentName = ActiveEnvironmentName,
                Key = "baseUrl",
                Value = "https://api.example.com",
                IsEnabled = true
            });
            EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
            {
                EnvironmentName = ActiveEnvironmentName,
                Key = "token",
                Value = "demo-token",
                IsEnabled = false
            });
        }
    }

    [RelayCommand]
    private void AddVariable()
    {
        EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
        {
            EnvironmentName = ActiveEnvironmentName,
            Key = string.Empty,
            Value = string.Empty,
            IsEnabled = true
        });
    }

    [RelayCommand]
    private async Task RemoveVariableAsync(EnvironmentVariableItemViewModel? item)
    {
        if (item is null) return;
        if (!string.IsNullOrWhiteSpace(item.Id))
            await _environmentVariableService.DeleteAsync(item.Id, CancellationToken.None);
        EnvironmentVariables.Remove(item);
    }

    [RelayCommand]
    private async Task SaveVariablesAsync()
    {
        foreach (var item in EnvironmentVariables)
        {
            var result = await _environmentVariableService.SaveAsync(new EnvironmentVariableDto
            {
                Id = item.Id,
                EnvironmentName = item.EnvironmentName,
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            }, CancellationToken.None);

            if (result.IsSuccess && result.Data is not null)
                item.Id = result.Data.Id;
        }

        await LoadVariablesAsync();
    }
}
