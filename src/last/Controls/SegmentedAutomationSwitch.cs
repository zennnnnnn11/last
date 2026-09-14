using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media.Transformation;

namespace last.Controls;

/// <summary>
///     仿苹果风格的现代化三态分段开关控件。
///     状态 0: 关闭（滑块停留在第 1 格）
///     状态 1: 自动接受（滑块停留在第 2 格）
///     状态 2: 自动秒开（滑块向右弹性拉伸，同时覆盖第 2 格和第 3 格）
/// </summary>
[PseudoClasses(":state0", ":state1", ":state2")]
[TemplatePart("PART_Thumb", typeof(Border))]
[TemplatePart("PART_Slot0", typeof(InputElement))]
[TemplatePart("PART_Slot1", typeof(InputElement))]
[TemplatePart("PART_Slot2", typeof(InputElement))]
public class SegmentedAutomationSwitch : TemplatedControl
{
    public static readonly StyledProperty<int> StateProperty =
        AvaloniaProperty.Register<SegmentedAutomationSwitch, int>(
            nameof(State),
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, val) => Math.Clamp(val, 0, 2));

    private InputElement? _slot0;
    private InputElement? _slot1;
    private InputElement? _slot2;
    private Border? _thumb;

    static SegmentedAutomationSwitch()
    {
        StateProperty.Changed.AddClassHandler<SegmentedAutomationSwitch>((x, e) =>
            x.OnStateChanged(e.GetNewValue<int>()));
    }

    public SegmentedAutomationSwitch()
    {
        Focusable = true;
        UpdatePseudoClasses(State);
    }

    public int State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_slot0 is not null)
            _slot0.PointerPressed -= OnSlot0PointerPressed;
        if (_slot1 is not null)
            _slot1.PointerPressed -= OnSlot1PointerPressed;
        if (_slot2 is not null)
            _slot2.PointerPressed -= OnSlot2PointerPressed;

        _thumb = e.NameScope.Find<Border>("PART_Thumb");
        _slot0 = e.NameScope.Find<InputElement>("PART_Slot0");
        _slot1 = e.NameScope.Find<InputElement>("PART_Slot1");
        _slot2 = e.NameScope.Find<InputElement>("PART_Slot2");

        if (_slot0 is not null)
            _slot0.PointerPressed += OnSlot0PointerPressed;
        if (_slot1 is not null)
            _slot1.PointerPressed += OnSlot1PointerPressed;
        if (_slot2 is not null)
            _slot2.PointerPressed += OnSlot2PointerPressed;

        UpdatePseudoClasses(State);
        UpdateThumb();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_slot0 is not null)
        {
            _slot0.PointerPressed -= OnSlot0PointerPressed;
            _slot0 = null;
        }

        if (_slot1 is not null)
        {
            _slot1.PointerPressed -= OnSlot1PointerPressed;
            _slot1 = null;
        }

        if (_slot2 is not null)
        {
            _slot2.PointerPressed -= OnSlot2PointerPressed;
            _slot2 = null;
        }

        _thumb = null;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateThumb();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Left)
        {
            State = Math.Max(0, State - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            State = Math.Min(2, State + 1);
            e.Handled = true;
        }
        else if (e.Key is Key.Space or Key.Enter)
        {
            State = (State + 1) % 3;
            e.Handled = true;
        }
    }

    private void OnSlot0PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        State = 0;
        Focus();
        e.Handled = true;
    }

    private void OnSlot1PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        State = 1;
        Focus();
        e.Handled = true;
    }

    private void OnSlot2PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        State = 2;
        Focus();
        e.Handled = true;
    }

    private void OnStateChanged(int newState)
    {
        UpdatePseudoClasses(newState);
        UpdateThumb();
    }

    private void UpdatePseudoClasses(int state)
    {
        PseudoClasses.Set(":state0", state == 0);
        PseudoClasses.Set(":state1", state == 1);
        PseudoClasses.Set(":state2", state == 2);
    }

    private void UpdateThumb()
    {
        if (_thumb is null)
            return;

        var totalWidth = Bounds.Width;
        if (totalWidth <= 0)
            return;

        const double trackPadding = 2.5;
        var innerWidth = Math.Max(0, totalWidth - trackPadding * 2);
        var slotWidth = innerWidth / 3.0;

        double targetX;
        double targetWidth;

        switch (State)
        {
            case 1:
                targetX = trackPadding + slotWidth;
                targetWidth = slotWidth;
                break;
            case 2:
                targetX = trackPadding + slotWidth;
                targetWidth = slotWidth * 2;
                break;
            default:
                targetX = trackPadding;
                targetWidth = slotWidth;
                break;
        }

        _thumb.Width = Math.Round(targetWidth, 1);
        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendTranslate(targetX, 0);
        _thumb.RenderTransform = builder.Build();
    }
}