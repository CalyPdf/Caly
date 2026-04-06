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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Services.Interfaces;
using Caly.Printing.Core;
using SharpIpp;
using SharpIpp.Models.Requests;
using SkiaSharp;

namespace Caly.Printing.Unix;

/// <summary>
/// Unix (Linux/macOS) print service.
/// <para>
/// Printer discovery tries CUPS IPP first, then falls back to <c>lpstat -a</c>.
/// Jobs are sent via SharpIppNext as JPEG images over IPP.
/// </para>
/// </summary>
public sealed class UnixPrintService : IPrintService, IDisposable
{
    // Re-used across all IPP calls for this service instance.
    private readonly SharpIppClient _ippClient = new();

    private static readonly Uri s_cupsLocalUri = new("ipp://localhost:631/");

    public void Dispose() => _ippClient.Dispose();

    // -------------------------------------------------------------------------
    // Printer discovery
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<PrinterInfo>> GetAvailablePrintersAsync(CancellationToken token = default)
    {
        try
        {
            var cupsPrinters = await GetCupsPrintersAsync(token).ConfigureAwait(false);
            if (cupsPrinters.Count > 0)
                return cupsPrinters;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UnixPrintService: CUPS discovery failed: {ex.Message}");
        }

        return await GetLpstatPrintersAsync(token).ConfigureAwait(false);
    }

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

            var uriStr = p.PrinterUriSupported?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (uriStr is null || !Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                uri = PrintServiceHelper.BuildCupsUri(name);

            result.Add(new PrinterInfo(name, uri));
        }

        return result;
    }

    private static async Task<IReadOnlyList<PrinterInfo>> GetLpstatPrintersAsync(CancellationToken token)
    {
        var output = await RunProcessAsync("lpstat", ["-a"], token).ConfigureAwait(false);

        var result = new List<PrinterInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = line.IndexOf(' ');
            var name = space > 0 ? line[..space] : line;
            if (!string.IsNullOrWhiteSpace(name))
                result.Add(new PrinterInfo(name, PrintServiceHelper.BuildCupsUri(name)));
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Printing
    // -------------------------------------------------------------------------

    public async Task PrintDocumentAsync(
        PrinterInfo printer,
        IPdfDocumentService documentService,
        IReadOnlyList<PrintPageInfo> pages,
        CancellationToken token = default)
    {
        // Render all pages to bitmaps first (async, independent of IPP delivery).
        var bitmaps = new SKBitmap?[pages.Count];
        try
        {
            for (int i = 0; i < pages.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                bitmaps[i] = await PrintServiceHelper.RenderPageToBitmapAsync(documentService, pages[i], token)
                    .ConfigureAwait(false);
            }

            await PrintIppAsync(printer, documentService.FileName, bitmaps, token).ConfigureAwait(false);
        }
        finally
        {
            foreach (var bmp in bitmaps)
                bmp?.Dispose();
        }
    }

    // -------------------------------------------------------------------------
    // Unix — IPP path (JPEG images)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends each page as a JPEG <c>image/jpeg</c> IPP document.
    /// Uses <c>PrintJob</c> for the first page and <c>SendDocument</c> for subsequent pages
    /// so that all pages land in a single print-queue entry.
    /// </summary>
    private async Task PrintIppAsync(
        PrinterInfo printer,
        string? documentName,
        SKBitmap?[] bitmaps,
        CancellationToken token)
    {
        string docName = documentName ?? "Document";
        int? jobId = null;

        for (int i = 0; i < bitmaps.Length; i++)
        {
            token.ThrowIfCancellationRequested();

            var bitmap = bitmaps[i];
            if (bitmap is null)
                continue;

            bool isLast = i == bitmaps.Length - 1
                          || Array.TrueForAll(bitmaps[(i + 1)..], b => b is null);

            using var jpegStream = PrintServiceHelper.EncodeJpeg(bitmap);

            if (jobId is null)
            {
                // First (or only) page — create the print job.
                var printRequest = new PrintJobRequest
                {
                    Document = jpegStream,
                    OperationAttributes = new PrintJobOperationAttributes
                    {
                        PrinterUri = printer.IppUri,
                        DocumentFormat = "image/jpeg",
                        DocumentName = docName
                    }
                };

                var printResponse = await _ippClient.PrintJobAsync(printRequest, token)
                    .ConfigureAwait(false);

                if ((short)printResponse.StatusCode >= 0x0100)
                {
                    throw new InvalidOperationException(
                        $"The printer rejected the job (IPP status {printResponse.StatusCode:X4}: {printResponse.StatusCode}).");
                }

                if (!isLast)
                    jobId = printResponse.JobAttributes?.JobId;
            }
            else
            {
                // Subsequent pages — append to the existing job via Send-Document.
                var sendRequest = new SendDocumentRequest
                {
                    Document = jpegStream,
                    OperationAttributes = new SendDocumentOperationAttributes
                    {
                        PrinterUri = printer.IppUri,
                        JobId = jobId.Value,
                        DocumentFormat = "image/jpeg",
                        LastDocument = isLast
                    }
                };

                var sendResponse = await _ippClient.SendDocumentAsync(sendRequest, token).ConfigureAwait(false);

                if ((short)sendResponse.StatusCode >= 0x0100)
                {
                    throw new InvalidOperationException(
                        $"Send-Document failed (IPP status {sendResponse.StatusCode:X4}: {sendResponse.StatusCode}).");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<string> RunProcessAsync(string command, string[] args, CancellationToken token)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
            return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync(token).ConfigureAwait(false);
        await process.WaitForExitAsync(token).ConfigureAwait(false);
        return output;
    }
}
