using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace last.Controls;

[TemplatePart("PART_PinButton", typeof(ToggleButton))]
[TemplatePart("PART_CollapseButton", typeof(Button))]
[TemplatePart("PART_CloseButton", typeof(Button))]
public class WindowActionCapsule : TemplatedControl
{
    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<WindowActionCapsule, bool>(
            nameof(IsPinned),
            defaultBindingMode: BindingMode.TwoWay);

    private Button? _closeButton;

    private Button? _collapseButton;
    private Window? _hostWindow;
    private bool _isSyncingTopmost;

    static WindowActionCapsule()
    {
        IsPinnedProperty.Changed.AddClassHandler<WindowActionCapsule>((x, e) =>
            x.OnIsPinnedChanged(e.GetNewValue<bool>()));
    }

    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    private void OnIsPinnedChanged(bool isPinned)
    {
        if (_isSyncingTopmost)
            return;

        if (GetHostWindow() is { } window && window.Topmost != isPinned)
        {
            _isSyncingTopmost = true;
            try
            {
                window.Topmost = isPinned;
            }
            finally
            {
                _isSyncingTopmost = false;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        DetachHostWindow();

        if (GetHostWindow() is { } window)
        {
            _hostWindow = window;
            _hostWindow.PropertyChanged += OnWindowPropertyChanged;

            if (IsSet(IsPinnedProperty))
                window.Topmost = IsPinned;
            else
                SetCurrentValue(IsPinnedProperty, window.Topmost);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachHostWindow();

        if (_collapseButton is not null)
        {
            _collapseButton.Click -= OnCollapseButtonClick;
            _collapseButton = null;
        }

        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseButtonClick;
            _closeButton = null;
        }
    }

    private void DetachHostWindow()
    {
        if (_hostWindow is not null)
        {
            _hostWindow.PropertyChanged -= OnWindowPropertyChanged;
            _hostWindow = null;
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowBase.TopmostProperty)
        {
            if (_isSyncingTopmost)
                return;

            _isSyncingTopmost = true;
            try
            {
                SetCurrentValue(IsPinnedProperty, e.GetNewValue<bool>());
            }
            finally
            {
                _isSyncingTopmost = false;
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_collapseButton is not null)
            _collapseButton.Click -= OnCollapseButtonClick;

        if (_closeButton is not null)
            _closeButton.Click -= OnCloseButtonClick;

        _collapseButton = e.NameScope.Find<Button>("PART_CollapseButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");

        if (_collapseButton is not null)
            _collapseButton.Click += OnCollapseButtonClick;

        if (_closeButton is not null)
            _closeButton.Click += OnCloseButtonClick;
    }

    private void OnCollapseButtonClick(object? sender, RoutedEventArgs e)
    {
        App.HideMainWindowToTray();
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        if (GetHostWindow() is { } window) _ = App.RequestExitAppAsync(window);
    }

    private Window? GetHostWindow()
    {
        return TopLevel.GetTopLevel(this) as Window;
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new WindowActionCapsuleAutomationPeer(this);
    }
}

public class WindowActionCapsuleAutomationPeer(WindowActionCapsule owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.ToolBar;
    }

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return !string.IsNullOrEmpty(name) ? name : "窗口操作栏";
    }
}