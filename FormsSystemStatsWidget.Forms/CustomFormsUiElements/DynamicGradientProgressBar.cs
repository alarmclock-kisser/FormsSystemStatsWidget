using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.ComponentModel;

public class DynamicGradientProgressBar : Control
{
    private int _value = 0;
    private int _minimum = 0;
    private int _maximum = 100;
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => this._minimum;
        set { this._minimum = value; this.Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => this._maximum;
        set { this._maximum = value; this.Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => this._value;
        set
        {
            // Begrenzen, um ArgumentExceptions zu verhindern
            this._value = Math.Max(this._minimum, Math.Min(value, this._maximum));
            this.Invalidate(); // Löst Neuzeichnen aus
        }
    }

    public DynamicGradientProgressBar()
    {
        // Aktiviert DoubleBuffering gegen Flackern und erlaubt transparentes/eigenes Zeichnen
        this.SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        // Standardgröße setzen
        this.Size = new Size(200, 15);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        // 1. Hintergrund zeichnen (Passend zu deinem hellgrauen Widget-Hintergrund)
        using (var bgBrush = new SolidBrush(Color.FromArgb(230, 230, 230)))
        {
            g.FillRectangle(bgBrush, this.ClientRectangle);
        }

        if (this._maximum <= this._minimum || this._value <= this._minimum) return;

        // 2. Berechnen, wie breit die Füllung sein muss
        float percent = (this._value - this._minimum) / (float)(this._maximum - this._minimum);
        int fillWidth = (int) (this.Width * percent);

        if (fillWidth > 0)
        {
            Rectangle fillRect = new(0, 0, fillWidth, this.Height);

            // 3. Fließenden Farbverlauf über ColorBlend generieren
            using (var brush = new LinearGradientBrush(this.ClientRectangle, Color.Black, Color.Black, 0f))
            {
                ColorBlend blend = new();

                // Deine Farbstufen (0% bis 100%)
                blend.Colors = new Color[] {
                    Color.FromArgb(50, 205, 50),   // 0%:   Grasgrün (LimeGreen)
                    Color.FromArgb(50, 205, 50),   // 20%:  Bleibt grasgrün
                    Color.FromArgb(255, 140, 0),  // 65%:  Gelb-Orange (DarkOrange)
                    Color.FromArgb(220, 20, 60)    // 100%: Signalrot (Crimson)
                };

                // Die exakten Positionen im Verlauf (Werte von 0.0 bis 1.0)
                blend.Positions = new float[] { 0.0f, 0.2f, 0.65f, 1.0f };
                brush.InterpolationColors = blend;

                // Nur den aktuell gefüllten Bereich mit dem Verlauf ausstanzen
                g.FillRectangle(brush, fillRect);
            }
        }

        using (var pen = new Pen(Color.FromArgb(180, 180, 180)))
        {
            g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
        }
    }
}