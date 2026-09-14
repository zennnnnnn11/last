using Avalonia.Controls;
using Avalonia.Input;
using last.ViewModels;

namespace last.Views;

public partial class MatchHistoryItemView : UserControl
{
    public MatchHistoryItemView()
    {
        InitializeComponent();
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MatchHistoryItemViewModel vm) vm.OpenDetailCommand.Execute(null);
    }
}