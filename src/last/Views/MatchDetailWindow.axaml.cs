using Avalonia.Controls;
using Avalonia.Interactivity;

namespace last.Views;

public partial class MatchDetailWindow : Window
{
    public MatchDetailWindow()
    {
        InitializeComponent();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}