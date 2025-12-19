// Copyright © 2017-2025 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace QuickLook.Plugin.PDFViewerNative;

public class Plugin : IViewer
{
    // Fallback preferred window size (DIPs). Real size is computed from the PDF first page when possible.
    private static double _width = 1000;
    private static double _height = 1200;

    public int Priority => 1;

    private WebpagePanel _panel;

    public void Init()
    {
    }

    public bool CanHandle(string path)
    {
        if (File.Exists(path) && Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    public void Prepare(string path, ContextObject context)
    {
        // Prefer a window aspect ratio matching the PDF's first page to avoid large blank areas.
        if (PdfPageInfo.TryGetFirstPageSizePoints(path, out var pwPt, out var phPt))
        {
            // Convert PDF points (1/72") to WPF DIPs (1/96"). Only the ratio matters here.
            var pwDip = pwPt * (96.0 / 72.0);
            var phDip = phPt * (96.0 / 72.0);

            // Use a stable base width and derive height from page ratio.
            const double baseWidth = 1100;
            var targetHeight = baseWidth * (phDip / pwDip);

            // Clamp to reasonable bounds to avoid extreme aspect ratios exploding the UI.
            targetHeight = Math.Max(500, Math.Min(1800, targetHeight));
            var targetWidth = Math.Max(700, Math.Min(1800, baseWidth));

            context.SetPreferredSizeFit(new Size(targetWidth, targetHeight), 0.9d);
        }
        else
        {
            context.SetPreferredSizeFit(new Size(_width, _height), 0.9d);
        }
    }

    public void View(string path, ContextObject context)
    {
        _panel = new WebpagePanel();
        context.ViewerContent = _panel;
        context.Title = Path.GetFileName(path);

        _panel.NavigateToFile(path);
        _panel.Dispatcher.Invoke(() => { context.IsBusy = false; }, DispatcherPriority.Loaded);
    }

    public void Cleanup()
    {
        _width = _panel.ActualWidth;
        _height = _panel.ActualHeight;

        _panel?.Dispose();
        _panel = null;

        GC.SuppressFinalize(this);
    }
}
