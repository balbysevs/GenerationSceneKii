using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using SceneGenerator.Desktop.Views;

namespace SceneGenerator.Desktop.Services;

public sealed class WindowConfirmationService : IConfirmationService
{
    private readonly Window _owner;

    public WindowConfirmationService(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        return new ConfirmationDialog(title, message).ShowDialog<bool>(_owner);
    }
}
