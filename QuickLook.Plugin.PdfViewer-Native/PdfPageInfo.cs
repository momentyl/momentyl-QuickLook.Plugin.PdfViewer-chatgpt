using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.PDFViewerNative;

/// <summary>
/// Lightweight PDF first-page size reader.
///
/// Notes:
/// - This is intentionally dependency-free and "good enough" for getting an aspect ratio.
/// - It parses the first visible /MediaBox in the first couple of MBs of the file.
/// - For PDFs where objects are stored only in compressed object streams (or encrypted),
///   this may fail; callers should fall back to a default.
/// </summary>
internal static class PdfPageInfo
{
    // 2MB is typically enough to capture the first page dictionary in most PDFs.
    private const int MaxProbeBytes = 2 * 1024 * 1024;

    private static readonly Regex MediaBoxRegex = new(
        @"/MediaBox\s*\[\s*(?<x0>-?\d+(?:\.\d+)?)\s+(?<y0>-?\d+(?:\.\d+)?)\s+(?<x1>-?\d+(?:\.\d+)?)\s+(?<y1>-?\d+(?:\.\d+)?)\s*\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RotateRegex = new(
        @"/Rotate\s+(?<rot>-?\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns first page width/height in PDF points (1 point = 1/72 inch).
    /// </summary>
    public static bool TryGetFirstPageSizePoints(string pdfPath, out double widthPt, out double heightPt)
    {
        widthPt = 0;
        heightPt = 0;

        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return false;

        try
        {
            byte[] buffer;
            using (var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var len = (int)Math.Min(fs.Length, MaxProbeBytes);
                buffer = new byte[len];
                var read = fs.Read(buffer, 0, len);
                if (read <= 0) return false;
                if (read < len)
                {
                    // Trim if file is smaller than probe length.
                    Array.Resize(ref buffer, read);
                }
            }

            // Use a single-byte encoding to preserve bytes without throwing.
            var text = Encoding.GetEncoding(28591).GetString(buffer); // ISO-8859-1

            var m = MediaBoxRegex.Match(text);
            if (!m.Success)
                return false;

            var x0 = ParseDoubleInvariant(m.Groups["x0"].Value);
            var y0 = ParseDoubleInvariant(m.Groups["y0"].Value);
            var x1 = ParseDoubleInvariant(m.Groups["x1"].Value);
            var y1 = ParseDoubleInvariant(m.Groups["y1"].Value);

            var w = Math.Abs(x1 - x0);
            var h = Math.Abs(y1 - y0);
            if (w < 1 || h < 1) return false;

            // Try to detect a nearby /Rotate entry. If rotated by 90/270, swap width/height.
            var rotate = TryGetNearbyRotate(text, m.Index);
            rotate = ((rotate % 360) + 360) % 360;
            if (rotate == 90 || rotate == 270)
            {
                (w, h) = (h, w);
            }

            widthPt = w;
            heightPt = h;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double ParseDoubleInvariant(string s)
        => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int TryGetNearbyRotate(string text, int mediaBoxIndex)
    {
        // Look backwards a limited range for /Rotate near the found MediaBox.
        const int window = 800;
        var start = Math.Max(0, mediaBoxIndex - window);
        var slice = text.Substring(start, Math.Min(window, mediaBoxIndex - start));

        // Use the last rotate value in the slice (closest above MediaBox).
        var matches = RotateRegex.Matches(slice);
        if (matches.Count == 0) return 0;

        var last = matches[matches.Count - 1];
        if (int.TryParse(last.Groups["rot"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rot))
            return rot;

        return 0;
    }
}
