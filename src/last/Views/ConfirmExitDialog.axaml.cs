using Avalonia.Controls;
using Avalonia.Interactivity;

namespace last.Views;

public partial class ConfirmExitDialog : Window
{
    public ConfirmExitDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}