using System.Drawing.Drawing2D;

namespace KIScheduler.WinForms;

internal enum UiIcon
{
    Add, Project, Edit, Duplicate, History, PriorityUp, PriorityDown,
    Pause, Play, Cancel, Review, Requeue, Refresh, Platform, Delete,
    Unlock, Copy, Policy, Settings, Pattern, Test, Save, Default
}

internal static class UiIcons
{
    private static readonly Color Ink = Color.FromArgb(37, 61, 86);
    private static readonly Color Accent = Color.FromArgb(30, 119, 180);
    private static readonly Color Positive = Color.FromArgb(36, 143, 103);
    private static readonly Color Warning = Color.FromArgb(191, 106, 43);

    public static Bitmap Create(UiIcon icon, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.ScaleTransform(size / 64f, size / 64f);
        using var ink = new Pen(Ink, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round };
        using var accent = new Pen(Accent, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round };
        using var positive = new Pen(Positive, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round };
        using var warning = new Pen(Warning, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round,
            LineJoin = LineJoin.Round };
        using var inkBrush = new SolidBrush(Ink);
        using var accentBrush = new SolidBrush(Accent);
        using var positiveBrush = new SolidBrush(Positive);
        using var warningBrush = new SolidBrush(Warning);

        void Plus(float x, float y, Pen pen)
        {
            graphics.DrawLine(pen, x - 8, y, x + 8, y);
            graphics.DrawLine(pen, x, y - 8, x, y + 8);
        }
        void Arrow(bool up, Pen pen)
        {
            var tip = up ? 13 : 51;
            var tail = up ? 49 : 15;
            graphics.DrawLine(pen, 32, tail, 32, tip);
            graphics.DrawLines(pen, new Point[] { (20, up ? 25 : 39).ToPoint(), (32, tip).ToPoint(),
                (44, up ? 25 : 39).ToPoint() });
        }
        void Document()
        {
            graphics.DrawRectangle(ink, 15, 10, 34, 44);
            graphics.DrawLine(ink, 22, 24, 42, 24);
            graphics.DrawLine(ink, 22, 32, 42, 32);
        }
        void Check()
        {
            graphics.DrawLines(positive, new Point[] { (14, 33).ToPoint(), (26, 44).ToPoint(),
                (50, 19).ToPoint() });
        }

        switch (icon)
        {
            case UiIcon.Add:
                graphics.DrawRectangle(ink, 12, 12, 40, 40);
                Plus(32, 32, accent);
                break;
            case UiIcon.Project:
                graphics.DrawLine(ink, 11, 21, 27, 21);
                graphics.DrawLine(ink, 27, 21, 32, 27);
                graphics.DrawLine(ink, 32, 27, 53, 27);
                graphics.DrawRectangle(ink, 11, 27, 42, 26);
                Plus(40, 40, positive);
                break;
            case UiIcon.Edit:
                Document();
                graphics.DrawLine(accent, 27, 47, 51, 23);
                graphics.DrawLines(accent, new Point[] { (26, 48).ToPoint(), (25, 54).ToPoint(),
                    (31, 53).ToPoint() });
                break;
            case UiIcon.Duplicate:
                graphics.DrawRectangle(ink, 10, 19, 34, 34);
                graphics.DrawRectangle(accent, 20, 10, 34, 34);
                break;
            case UiIcon.History:
                graphics.DrawArc(accent, 12, 12, 40, 40, -75, 310);
                graphics.DrawLines(accent, new Point[] { (11, 17).ToPoint(), (12, 30).ToPoint(),
                    (25, 27).ToPoint() });
                graphics.DrawLines(ink, new Point[] { (32, 20).ToPoint(), (32, 33).ToPoint(),
                    (42, 39).ToPoint() });
                break;
            case UiIcon.PriorityUp: Arrow(true, accent); break;
            case UiIcon.PriorityDown: Arrow(false, accent); break;
            case UiIcon.Pause:
                graphics.FillRectangle(warningBrush, 19, 16, 9, 32);
                graphics.FillRectangle(warningBrush, 36, 16, 9, 32);
                break;
            case UiIcon.Play:
                graphics.FillPolygon(positiveBrush, new Point[] { (21, 14).ToPoint(), (21, 50).ToPoint(),
                    (49, 32).ToPoint() });
                break;
            case UiIcon.Cancel:
                graphics.DrawEllipse(warning, 12, 12, 40, 40);
                graphics.DrawLine(warning, 19, 19, 45, 45);
                break;
            case UiIcon.Review:
                graphics.DrawEllipse(ink, 10, 21, 44, 23);
                graphics.FillEllipse(accentBrush, 26, 25, 12, 12);
                Check();
                break;
            case UiIcon.Requeue:
                graphics.DrawArc(accent, 12, 12, 40, 40, -80, 300);
                graphics.DrawLines(accent, new Point[] { (12, 22).ToPoint(), (13, 35).ToPoint(),
                    (26, 32).ToPoint() });
                break;
            case UiIcon.Refresh:
                graphics.DrawArc(accent, 12, 12, 40, 40, -45, 155);
                graphics.DrawArc(positive, 12, 12, 40, 40, 135, 155);
                graphics.DrawLines(accent, new Point[] { (47, 11).ToPoint(), (52, 24).ToPoint(),
                    (39, 25).ToPoint() });
                graphics.DrawLines(positive, new Point[] { (17, 53).ToPoint(), (12, 40).ToPoint(),
                    (25, 39).ToPoint() });
                break;
            case UiIcon.Platform:
                graphics.DrawRectangle(ink, 10, 14, 44, 32);
                graphics.DrawLine(ink, 32, 46, 32, 53);
                graphics.DrawLine(ink, 22, 53, 42, 53);
                graphics.FillEllipse(positiveBrush, 18, 25, 8, 8);
                graphics.FillEllipse(accentBrush, 30, 25, 8, 8);
                break;
            case UiIcon.Delete:
                graphics.DrawRectangle(warning, 18, 20, 28, 34);
                graphics.DrawLine(warning, 14, 18, 50, 18);
                graphics.DrawLine(warning, 25, 11, 39, 11);
                graphics.DrawLine(warning, 27, 28, 27, 45);
                graphics.DrawLine(warning, 37, 28, 37, 45);
                break;
            case UiIcon.Unlock:
                graphics.DrawArc(ink, 19, 8, 26, 34, 170, 190);
                graphics.DrawRectangle(accent, 14, 30, 36, 24);
                graphics.FillEllipse(accentBrush, 29, 40, 6, 6);
                break;
            case UiIcon.Copy:
                graphics.DrawRectangle(ink, 12, 17, 32, 37);
                graphics.DrawRectangle(accent, 22, 10, 32, 37);
                graphics.DrawLine(accent, 29, 24, 46, 24);
                graphics.DrawLine(accent, 29, 32, 46, 32);
                break;
            case UiIcon.Policy:
                Document();
                Check();
                break;
            case UiIcon.Settings:
                graphics.DrawEllipse(ink, 13, 13, 38, 38);
                graphics.DrawEllipse(accent, 24, 24, 16, 16);
                for (var angle = 0; angle < 360; angle += 45)
                {
                    var radians = angle * Math.PI / 180;
                    graphics.DrawLine(ink, 32 + (float)Math.Cos(radians) * 21,
                        32 + (float)Math.Sin(radians) * 21, 32 + (float)Math.Cos(radians) * 27,
                        32 + (float)Math.Sin(radians) * 27);
                }
                break;
            case UiIcon.Pattern:
                graphics.DrawLine(ink, 14, 16, 14, 48);
                graphics.DrawLine(ink, 50, 16, 50, 48);
                graphics.DrawLine(accent, 23, 33, 41, 33);
                graphics.DrawLine(accent, 32, 23, 32, 43);
                break;
            case UiIcon.Test:
                graphics.DrawEllipse(ink, 11, 11, 42, 42);
                graphics.DrawLine(accent, 32, 17, 32, 35);
                Check();
                break;
            case UiIcon.Save:
                graphics.DrawRectangle(ink, 10, 10, 44, 44);
                graphics.DrawRectangle(accent, 19, 11, 26, 15);
                graphics.DrawRectangle(ink, 20, 38, 24, 16);
                break;
            case UiIcon.Default:
                var points = new Point[10];
                for (var index = 0; index < points.Length; index++)
                {
                    var angle = -Math.PI / 2 + index * Math.PI / 5;
                    var radius = index % 2 == 0 ? 24 : 11;
                    points[index] = new Point(32 + (int)(Math.Cos(angle) * radius),
                        32 + (int)(Math.Sin(angle) * radius));
                }
                graphics.FillPolygon(accentBrush, points);
                break;
        }
        return bitmap;
    }

    private static Point ToPoint(this (int X, int Y) point) => new(point.X, point.Y);
}

internal sealed class ToolbarColorTable : ProfessionalColorTable
{
    public override Color ToolStripGradientBegin => Color.White;
    public override Color ToolStripGradientMiddle => Color.White;
    public override Color ToolStripGradientEnd => Color.White;
    public override Color ButtonSelectedGradientBegin => Color.FromArgb(224, 239, 250);
    public override Color ButtonSelectedGradientMiddle => Color.FromArgb(224, 239, 250);
    public override Color ButtonSelectedGradientEnd => Color.FromArgb(224, 239, 250);
    public override Color ButtonSelectedBorder => Color.FromArgb(144, 192, 224);
}
