using System.Drawing.Drawing2D;

namespace WardogsBridge;

internal sealed class RadioMeter : Control
{
    private int value;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value { get => value; set { this.value = Math.Clamp(value, 0, 100); Invalidate(); } }
    public RadioMeter() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(43, 56, 52));
        int filled = Width * value / 100;
        if (filled <= 0 || Height <= 0) return;
        using var color = new LinearGradientBrush(ClientRectangle, Color.FromArgb(103, 166, 137), Color.FromArgb(215, 187, 113), 0f);
        e.Graphics.FillRectangle(color, 0, 0, filled, Height);
    }
}
