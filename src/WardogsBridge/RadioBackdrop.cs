using System.Drawing.Drawing2D;

namespace WardogsBridge;

internal sealed class RadioBackdrop : Panel
{
    private readonly Image? art;
    public RadioBackdrop()
    {
        DoubleBuffered = true;
        using var stream = typeof(RadioBackdrop).Assembly.GetManifestResourceStream("WardogsBridge.Assets.vehicle-radio.png");
        if (stream != null) { using var source = Image.FromStream(stream); art = new Bitmap(source); }
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(13, 19, 21));
        if (art != null)
        {
            float scale = Math.Max((float)Width / art.Width, (float)Height / art.Height);
            float w = art.Width * scale, h = art.Height * scale;
            e.Graphics.DrawImage(art, Width - w, (Height - h) / 2, w, h);
        }
        if (Width > 0 && Height > 0)
        {
            using var shade = new LinearGradientBrush(ClientRectangle, Color.FromArgb(225, 8, 15, 17), Color.FromArgb(45, 8, 15, 17), 0f);
            e.Graphics.FillRectangle(shade, ClientRectangle);
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) art?.Dispose(); base.Dispose(disposing); }
}
