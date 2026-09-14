using Avalonia.Controls;
using Avalonia.Input;
using last.ViewModels;

namespace last.Views;

public partial class PlayerRowView : UserControl
{
    public PlayerRowView()
    {
        InitializeComponent();
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PlayerRowViewModel vm) vm.ToggleSelectCommand.Execute(null);
    }
}