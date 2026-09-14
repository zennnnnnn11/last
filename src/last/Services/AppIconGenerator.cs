using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace last.Services;

/// <summary>
///     应用程序高质量矢量徽标与托盘图标生成器（纯托管、零外部资产依赖、全平台与 AOT 安全）。
/// </summary>
public static class AppIconGenerator
{
    private static WindowIcon? _cachedIcon;

    public static WindowIcon GetOrCreateAppIcon()
    {
        if (_cachedIcon != null)
            return _cachedIcon;

        // 生成 48x48 高清位图图标
        var rtb = new RenderTargetBitmap(new PixelSize(48, 48), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            // 1. 绘制暗黑悬浮卡片圆角背景 (24, 24, 28)
            var bgRect = new Rect(2, 2, 44, 44);
            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(24, 24, 28)),
                new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.5),
                new RoundedRect(bgRect, 10));

            // 2. 绘制品牌高亮 "L" 极简标志 (38, 189, 248 - 鲜亮电竞青蓝)
            var streamGeometry = new StreamGeometry();
            using (var sgc = streamGeometry.Open())
            {
                sgc.BeginFigure(new Point(16, 13));
                sgc.LineTo(new Point(23, 13));
                sgc.LineTo(new Point(23, 30));
                sgc.LineTo(new Point(33, 30));
                sgc.LineTo(new Point(33, 35));
                sgc.LineTo(new Point(16, 35));
                sgc.EndFigure(true);
            }

            ctx.DrawGeometry(new SolidColorBrush(Color.FromRgb(56, 189, 248)), null, streamGeometry);

            // 3. 右上角高亮能量呼吸微圆点
            ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(241, 245, 249)), null, new Point(33, 16), 2.5, 2.5);
        }

        _cachedIcon = new WindowIcon(rtb);
        return _cachedIcon;
    }
}