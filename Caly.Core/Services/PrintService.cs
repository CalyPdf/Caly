// Copyright (c) 2025 BobLd
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Services.Interfaces;
using SharpIpp;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using SkiaSharp;

namespace Caly.Core.Services;

/// <summary>
/// Cross-platform print service built on SharpIppNext (IPP protocol).
/// <para>
/// Printer discovery uses the CUPS IPP extension on all platforms, with a PowerShell
/// fallback on Windows when CUPS is not available.  The print job is rendered entirely
/// in-memory (no temporary files) using SkiaSharp and sent over IPP via SharpIppNext.
/// </para>
/// </summary>
internal sealed class PrintService : IPrintService, IDisposable
{
    // Re-use a single client (and its internal HttpClient) for the process lifetime.
    private readonly SharpIppClient _ippClient = new();

    private static readonly Uri s_cupsLocalUri = new("ipp://localhost:631/");

    public void Dispose() => _ippClient.Dispose();

    // -------------------------------------------------------------------------
    // Printer discovery
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<PrinterInfo>> GetAvailablePrintersAsync(CancellationToken token = default)
    {
        // Try CUPS first — works natively on Linux/macOS and on Windows when CUPS
        // (or a compatible server) is installed.
        try
        {
            var cupsPrinters = await GetCupsPrintersAsync(token).ConfigureAwait(false);
            if (cupsPrinters.Count > 0)
                return cupsPrinters;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PrintService: CUPS discovery failed: {ex.Message}");
        }

        // Fallback: enumerate printers through the OS and construct IPP URIs.
        if (OperatingSystem.IsWindows())
            return await GetWindowsPrintersAsync(token).ConfigureAwait(false);

        // On Linux/macOS without CUPS, fall back to lpstat.
        return await GetLpstatPrintersAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries the local CUPS server via SharpIppNext and returns each printer's
    /// display name plus its first supported IPP URI.
    /// </summary>
    private async Task<IReadOnlyList<PrinterInfo>> GetCupsPrintersAsync(CancellationToken token)
    {
        var request = new CUPSGetPrintersRequest
        {
            OperationAttributes = new CUPSGetPrintersOperationAttributes
            {
                PrinterUri = s_cupsLocalUri
            }
        };

        var response = await _ippClient.GetCUPSPrintersAsync(request, token).ConfigureAwait(false);

        var attrs = response.PrintersAttributes;
        if (attrs is null || attrs.Length == 0)
            return [];

        var result = new List<PrinterInfo>(attrs.Length);
        foreach (var p in attrs)
        {
            var name = p.PrinterName;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            // PrinterUriSupported is string[] in SharpIppNext 3.x.
            var uriStr = p.PrinterUriSupported?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (uriStr is null || !Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
            {
                // Build a best-effort URI from the printer name.
                uri = BuildCupsUri(name);
            }

            result.Add(new PrinterInfo(name, uri));
        }

        return result;
    }

    /// <summary>
    /// Enumerates printers on Windows using PowerShell and constructs IPP URIs by
    /// inspecting the printer port for a host address (for network printers) or falling
    /// back to the local CUPS-compatible URI for local printers.
    /// </summary>
    private static async Task<IReadOnlyList<PrinterInfo>> GetWindowsPrintersAsync(CancellationToken token)
    {
        // One-liner that outputs "Name|ipp://host:port/" or "Name|ipp://localhost:631/printers/Name"
        // per printer. PS5-compatible (no $null-conditional operators).
        const string script = """
            Get-Printer | ForEach-Object {
                $p = $_
                $uri = $null
                try {
                    $port = Get-PrinterPort -Name $p.PortName -ErrorAction Stop
                    if ($port -ne $null -and
                        $port.PSObject.Properties.Name -contains 'PrinterHostAddress' -and
                        $port.PrinterHostAddress) {
                        $portNum = 631
                        if ($port.PSObject.Properties.Name -contains 'PortNumber' -and
                            $port.PortNumber -gt 0) {
                            $portNum = $port.PortNumber
                        }
                        $uri = 'ipp://' + $port.PrinterHostAddress + ':' + $portNum + '/'
                    }
                } catch {}
                if (-not $uri) {
                    $uri = 'ipp://localhost:631/printers/' + [uri]::EscapeDataString($p.Name)
                }
                $p.Name + '|' + $uri
            }
            """;

        var output = await RunProcessAsync(
            "powershell",
            ["-NoProfile", "-NonInteractive", "-Command", script],
            token).ConfigureAwait(false);

        return ParsePipeSeparatedPrinters(output);
    }

    /// <summary>
    /// Fallback for non-CUPS Linux/macOS: runs <c>lpstat -a</c> and constructs
    /// a CUPS IPP URI for each printer.
    /// </summary>
    private static async Task<IReadOnlyList<PrinterInfo>> GetLpstatPrintersAsync(CancellationToken token)
    {
        var output = await RunProcessAsync("lpstat", ["-a"], token).ConfigureAwait(false);

        var result = new List<PrinterInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = line.IndexOf(' ');
            var name = space > 0 ? line[..space] : line;
            if (!string.IsNullOrWhiteSpace(name))
                result.Add(new PrinterInfo(name, BuildCupsUri(name)));
        }

        return result;
    }

    private static IReadOnlyList<PrinterInfo> ParsePipeSeparatedPrinters(string output)
    {
        var result = new List<PrinterInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pipe = line.IndexOf('|');
            if (pipe <= 0)
                continue;

            var name = line[..pipe].Trim();
            var uriStr = line[(pipe + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(name) &&
                Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
            {
                result.Add(new PrinterInfo(name, uri));
            }
        }
        return result;
    }

    private static Uri BuildCupsUri(string printerName)
        => new($"ipp://localhost:631/printers/{Uri.EscapeDataString(printerName)}");

    // -------------------------------------------------------------------------
    // Printing
    // -------------------------------------------------------------------------

    public async Task PrintDocumentAsync(
        PrinterInfo printer,
        IPdfDocumentService documentService,
        IReadOnlyList<PrintPageInfo> pages,
        CancellationToken token = default)
    {
        // Render all selected pages into a MemoryStream as a PDF document.
        // No file is ever written to disk.
        using var pdfStream = new MemoryStream();
        await RenderToPdfStreamAsync(pdfStream, documentService, pages, token).ConfigureAwait(false);
        pdfStream.Position = 0;

        // Send the PDF bytes directly to the printer via IPP.
        var request = new PrintJobRequest
        {
            Document = pdfStream,
            OperationAttributes = new PrintJobOperationAttributes
            {
                PrinterUri = printer.IppUri,
                DocumentFormat = "application/pdf",
                DocumentName = documentService.FileName ?? "Document"
            }
        };

        var response = await _ippClient.PrintJobAsync(request, token).ConfigureAwait(false);

        // 0x0000–0x00FF are the IPP success range (RFC 8011).
        if ((short)response.StatusCode >= 0x0100)
        {
            throw new InvalidOperationException(
                $"The printer rejected the job (IPP status {response.StatusCode:X4}: {response.StatusCode}).");
        }
    }

    // -------------------------------------------------------------------------
    // Skia rendering — in-memory only, no temp files
    // -------------------------------------------------------------------------

    private static async Task RenderToPdfStreamAsync(
        MemoryStream destination,
        IPdfDocumentService documentService,
        IReadOnlyList<PrintPageInfo> pages,
        CancellationToken token)
    {
        // PDF rendering is CPU-intensive; move it off the UI thread.
        await Task.Run(async () =>
        {
            float ppiScale = (float)documentService.PpiScale;
            // Scale from PpiScale (144 dpi) coordinate space back to PDF point space (72 dpi).
            float scale = 1.0f / ppiScale;

            using var document = SKDocument.CreatePdf(destination);

            foreach (var pageInfo in pages)
            {
                token.ThrowIfCancellationRequested();

                var pageSize = await documentService.GetPageSizeAsync(pageInfo.PageNumber, token)
                    .ConfigureAwait(false);
                if (pageSize is null)
                    continue;

                using var picRef = await documentService.GetRenderPageAsync(pageInfo.PageNumber, token)
                    .ConfigureAwait(false);
                if (picRef is null)
                    continue;

                float pdfW = (float)pageSize.Value.Width;   // in points (1/72 inch)
                float pdfH = (float)pageSize.Value.Height;
                int rotation = pageInfo.Rotation;

                // Swap page dimensions for 90°/270° rotations so the page fits correctly.
                float pageW = (rotation == 90 || rotation == 270) ? pdfH : pdfW;
                float pageH = (rotation == 90 || rotation == 270) ? pdfW : pdfH;

                // BeginPage returns a canvas owned by the document – do NOT dispose it.
                var canvas = document.BeginPage(pageW, pageH);

                // Apply user rotation so the page prints upright.
                switch (rotation)
                {
                    case 90:
                        canvas.Translate(pageW, 0);
                        canvas.RotateDegrees(90);
                        break;
                    case 180:
                        canvas.Translate(pageW, pageH);
                        canvas.RotateDegrees(180);
                        break;
                    case 270:
                        canvas.Translate(0, pageH);
                        canvas.RotateDegrees(270);
                        break;
                }

                // Scale the SKPicture (recorded at PpiScale / 144 dpi) into PDF points (72 dpi).
                canvas.Scale(scale, scale);
                canvas.DrawPicture(picRef.Item);

                document.EndPage();
            }

            // Flush and finalise the PDF. Must be called before reading the stream.
            document.Close();
        }, token).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<string> RunProcessAsync(string command, string[] args, CancellationToken token)
    {
        var psi = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null)
            return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync(token).ConfigureAwait(false);
        await process.WaitForExitAsync(token).ConfigureAwait(false);
        return output;
    }
}
