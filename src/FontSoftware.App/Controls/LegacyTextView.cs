using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FontSoftware.App.Controls;

/// <summary>
/// Draws legacy 8-bit text by glyph index instead of by character.
///
/// A legacy font puts its letters on byte values that Unicode has already given
/// a meaning to, and WPF honours the Unicode meaning. ISFOC Gujarati places ર on
/// byte 0xAD — U+00AD SOFT HYPHEN — so a TextBox draws nothing at all for it and
/// "ભારત" appears on screen as "ભાત". Measured, not assumed: A-0xAD-A and AA come
/// out at exactly the same width in FormattedText and in a TextBox, in both
/// formatting modes. Word draws the glyph correctly, so the converted text itself
/// is right; it was only our own preview that lied about it.
///
/// Looking each character up in the font's own cmap and emitting a GlyphRun skips
/// character semantics entirely, which is what a legacy font has always needed.
/// Read-only by nature — the Copy button works off the exact text, never off what
/// is drawn here.
/// </summary>
public sealed class LegacyTextView : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(LegacyTextView),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(LegacyTextView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(LegacyTextView),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(LegacyTextView),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontFamily? FontFamily
    {
        get => (FontFamily?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private sealed record Line(List<ushort> Glyphs, List<double> Advances, double Width);

    private GlyphTypeface? _gtf;
    private List<Line> _lines = new();

    private bool Resolve()
    {
        var family = FontFamily;
        if (family is null) { _gtf = null; return false; }
        foreach (var tf in family.GetTypefaces())
            if (tf.TryGetGlyphTypeface(out var g)) { _gtf = g; return true; }
        _gtf = null;
        return false;
    }

    /// <summary>
    /// Lay the text out into lines, breaking at the last space that fits. A legacy
    /// font's advance widths come from the font itself, so the measured width here
    /// is the width that will actually be drawn.
    /// </summary>
    private void Layout(double maxWidth)
    {
        _lines = new List<Line>();
        if (!Resolve() || _gtf is null || string.IsNullOrEmpty(Text)) return;

        double size = FontSize;
        foreach (string paragraph in Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var glyphs = new List<ushort>();
            var advances = new List<double>();
            double width = 0;
            int lastBreak = -1;          // index in `glyphs` just after the last space
            double widthAtBreak = 0;

            foreach (char ch in paragraph)
            {
                if (!_gtf.CharacterToGlyphMap.TryGetValue(ch, out ushort gid))
                {
                    // Not in this font: a space keeps the word break, anything else
                    // is dropped rather than drawn as a stray box.
                    if (ch != ' ') continue;
                    gid = _gtf.CharacterToGlyphMap.TryGetValue(' ', out ushort sp) ? sp : (ushort)0;
                }
                double adv = _gtf.AdvanceWidths.TryGetValue(gid, out double a) ? a * size : 0;

                if (maxWidth > 0 && width + adv > maxWidth && lastBreak > 0)
                {
                    var head = glyphs.GetRange(0, lastBreak);
                    var headAdv = advances.GetRange(0, lastBreak);
                    _lines.Add(new Line(head, headAdv, widthAtBreak));
                    glyphs.RemoveRange(0, lastBreak);
                    advances.RemoveRange(0, lastBreak);
                    width -= widthAtBreak;
                    lastBreak = -1;
                }

                glyphs.Add(gid);
                advances.Add(adv);
                width += adv;
                if (ch == ' ') { lastBreak = glyphs.Count; widthAtBreak = width; }
            }
            _lines.Add(new Line(glyphs, advances, width));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double max = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        Layout(max);
        if (_gtf is null) return new Size(0, 0);
        double w = 0;
        foreach (var l in _lines) w = Math.Max(w, l.Width);
        return new Size(max > 0 ? Math.Min(max, w) : w, Math.Max(1, _lines.Count) * FontSize * _gtf.Height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_gtf is null || _lines.Count == 0) return;
        double size = FontSize;
        double lineHeight = size * _gtf.Height;
        double y = size * _gtf.Baseline;

        foreach (var line in _lines)
        {
            if (line.Glyphs.Count > 0)
            {
                // pixelsPerDip of 1 keeps this independent of the monitor; WPF
                // scales the drawing itself.
                var run = new GlyphRun(_gtf, 0, false, size, 1.0f,
                    line.Glyphs, new Point(0, y), line.Advances,
                    null, null, null, null, null, null);
                dc.DrawGlyphRun(Foreground, run);
            }
            y += lineHeight;
        }
    }
}
