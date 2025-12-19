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

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.Helpers;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.PDFViewerNative;

public class WebpagePanel : UserControl
{
    private Uri _currentUri;
    private WebView2 _webView;

    // PDF first-page size in points (1/72") for auto-fit zoom.
    private double _pdfWPt;
    private double _pdfHPt;
    private bool _hasPdfSize;

    public WebpagePanel()
    {
        if (!Helper.IsWebView2Available())
        {
            Content = CreateDownloadButton();
        }
        else
        {
            _webView = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\\"),
                },
                DefaultBackgroundColor = OSThemeHelper.AppsUseDarkTheme() ? Color.FromArgb(255, 32, 32, 32) : Color.White, // Prevent white flash in dark mode
            };
            _webView.NavigationStarting += NavigationStarting_CancelNavigation;
            _webView.NavigationCompleted += WebView_NavigationCompleted;

            // Re-fit when the preview window is resized.
            SizeChanged += (_, __) => TryFitPdfToViewport();
            Content = _webView;
        }
    }

    public void NavigateToFile(string path)
    {
        var uri = Path.IsPathRooted(path) ? Helper.FilePathToFileUrl(path) : new Uri(path);

        NavigateToUri(uri);
    }

    public void NavigateToUri(Uri uri)
    {
        if (_webView == null)
            return;

        // Cache PDF first-page size (if applicable) so we can auto-fit the content.
        CachePdfFirstPageSize(uri);

        _webView.Source = uri;
        _currentUri = _webView.Source;
    }

    public void NavigateToHtml(string html)
    {
        _webView?.EnsureCoreWebView2Async()
            .ContinueWith(_ => Dispatcher.Invoke(() => _webView?.NavigateToString(html)));
    }

    private void NavigationStarting_CancelNavigation(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("data:")) // when using NavigateToString
            return;

        var newUri = new Uri(e.Uri);
        if (newUri == _currentUri) return;
        e.Cancel = true;
    }

    private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _webView.DefaultBackgroundColor = Color.White; // Reset to white after page load to match expected default behavior

        // After the built-in PDF viewer loads, set a zoom factor that fits the PDF page to the available viewport.
        TryFitPdfToViewport();
    }

    public void Dispose()
    {
        _webView?.Dispose();
        _webView = null;
    }

    private void CachePdfFirstPageSize(Uri uri)
    {
        _hasPdfSize = false;
        _pdfWPt = 0;
        _pdfHPt = 0;

        try
        {
            if (uri == null) return;

            // Only handle local PDF files for auto-fit sizing.
            if (!uri.IsFile) return;
            var path = uri.LocalPath;
            if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return;

            if (PdfPageInfo.TryGetFirstPageSizePoints(path, out var wPt, out var hPt))
            {
                _pdfWPt = wPt;
                _pdfHPt = hPt;
                _hasPdfSize = true;
            }
        }
        catch
        {
            _hasPdfSize = false;
        }
    }

    private void TryFitPdfToViewport()
    {
        if (_webView == null || !_hasPdfSize) return;
        if (_webView.ActualWidth <= 0 || _webView.ActualHeight <= 0) return;

        // Convert PDF points (1/72") to DIPs (1/96") for a consistent scale basis.
        var pageWDip = _pdfWPt * (96.0 / 72.0);
        var pageHDip = _pdfHPt * (96.0 / 72.0);
        if (pageWDip <= 0 || pageHDip <= 0) return;

        // Heuristic padding factor: the built-in viewer has some chrome/margins.
        var viewW = Math.Max(1.0, _webView.ActualWidth * 0.98);
        var viewH = Math.Max(1.0, _webView.ActualHeight * 0.98);

        // Default behavior: fit-to-width, so the PDF uses the full available width and avoids large side blanks.
        // Height can overflow normally; the built-in viewer provides scrolling.
        var zoom = viewW / pageWDip;
        zoom = Math.Max(0.1, Math.Min(5.0, zoom));

        try
        {
            // Setting ZoomFactor is safe once the control exists; for some WebView2 builds it takes effect immediately.
            _webView.ZoomFactor = zoom;
        }
        catch
        {
            // Ignore: some environments may block programmatic zoom.
        }
    }

    private object CreateDownloadButton()
    {
        string translationFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations.config");

        var button = new Button
        {
            Content = TranslationHelper.Get("WEBVIEW2_NOT_AVAILABLE", translationFile),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(20, 6, 20, 6)
        };
        button.Click += (sender, e) => Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");

        return button;
    }
}
