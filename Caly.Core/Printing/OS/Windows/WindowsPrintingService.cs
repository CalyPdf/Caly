#if WINDOWS
using System;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Printing.Models;
using Caly.Core.Printing.Services;
using Vanara.PInvoke;

namespace Caly.Core.Printing.OS.Windows
{
    public class WindowsPrintingService : BasePrintingService
    {
        public override bool IsSupported => true;

        [SupportedOSPlatform("windows")]
        public override IEnumerable<CalyPrinterDevice> GetPrinters()
        {
            foreach (var printer in WinSpool.EnumPrinters<WinSpool.PRINTER_INFO_2>())
            {
                yield return new CalyPrinterDevice()
                {
                    Datatype = printer.pDatatype,
                    DriverName = printer.pDriverName,
                    IsOnline = !printer.Attributes.HasFlag(WinSpool.PRINTER_ATTRIBUTE.PRINTER_ATTRIBUTE_WORK_OFFLINE),
                    Name = printer.pPrinterName,
                    PortName = printer.pPortName,
                    ServerName = printer.pServerName,
                    IsMonochrome = printer.DevMode.dmColor == DMCOLOR.DMCOLOR_MONOCHROME
                };
            }
        }

        private const string DataType = "RAW";

        [SupportedOSPlatform("windows")]
        protected override async Task Print(CalyPrintJob printJob)
        {
            WinSpool.SafeHPRINTER? phPrinter = null;

            try
            {
                if (!WinSpool.OpenPrinter(printJob.PrinterName, out phPrinter))
                {
                    // Error
                    return;
                }

                var docInfo = new WinSpool.DOC_INFO_1() { pDatatype = DataType, pDocName = printJob.DocumentName };
                uint printJobId = WinSpool.StartDocPrinter(phPrinter, 1, docInfo);
                if (printJobId <= 0)
                {
                    // Error
                    return;
                }

                var pdfDocument = printJob.PdfDocument;
                int docPagesCount = pdfDocument.PageCount;

                using (var ms = new MemoryStream())
                using (var document = SKDocument.CreateXps(ms))
                {
                    foreach (var range in printJob.PagesRanges)
                    {
                        for (int p = range.Start.GetOffset(docPagesCount); p < range.End.GetOffset(docPagesCount); ++p)
                        {
                            var page = pdfDocument.Pages[p - 1];
                            var picture = page.PdfPicture?.Clone();
                            if (picture is null)
                            {
                                picture = await pdfDocument.PdfService.GetRenderPageAsync(p, CancellationToken.None);
                            }

                            if (picture is null)
                            {
                                throw new Exception();
                            }

                            using (picture)
                            {
                                float width = picture.Item.CullRect.Width;
                                float height = picture.Item.CullRect.Height;

                                using (var canvas = document.BeginPage(width, height))
                                {
                                    canvas.DrawPicture(picture.Item);
                                }
                            }

                            document.EndPage();
                        }
                    }

                    document.Close();

                    byte[] xpsBytes = ms.ToArray();
                    GCHandle handle = GCHandle.Alloc(xpsBytes, GCHandleType.Pinned);
                    try
                    {
                        nint ptr = handle.AddrOfPinnedObject();
                        if (WinSpool.WritePrinter(phPrinter, ptr, (uint)xpsBytes.Length, out uint pcWritten))
                        {
                            if (pcWritten == xpsBytes.Length)
                            {
                                // success
                            }
                            else
                            {
                                // error
                            }
                        }
                        else
                        {
                            // error
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                
                WinSpool.EndDocPrinter(phPrinter);
            }
            finally
            {
                if (phPrinter is not null)
                {
                    WinSpool.ClosePrinter(phPrinter);
                }
            }
        }
    }
}
#endif