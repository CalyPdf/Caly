using Caly.Printing.Models;
using SkiaSharp;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace Caly.Printing.Services
{
    public class WindowsPrintingService : BasePrintingService
    {
        public WindowsPrintingService() : base()
        {
            
        }

        public override bool IsSupported => true;

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
                    ServerName = printer.pServerName
                };
            }
        }

        private const string DataType = "RAW";

        protected override void Print(CalyPrintJob printJob)
        {
            WinSpool.SafeHPRINTER? phPrinter = null;

            SKPicture[] pictures = [];
            
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

                foreach (var picture in pictures)
                {
                    using (var ms = GetXpStream(picture))
                    {
                        byte[] xpsBytes = ms.ToArray();

                        if (!WinSpool.StartPagePrinter(phPrinter))
                        {
                            // Error
                            return;
                        }

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
                            WinSpool.EndPagePrinter(phPrinter);
                            handle.Free();
                        }
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

        private MemoryStream GetXpStream(SKPicture picture)
        {
            // One xps document per page
            var ms = new MemoryStream();
            using (var document = SKDocument.CreateXps(ms))
            {
                //foreach (var picture in pictures)
                //{
                float width = picture.CullRect.Width;
                float height = picture.CullRect.Height;

                using (var canvas = document.BeginPage(width, height))
                {
                    canvas.DrawPicture(picture);
                }

                document.EndPage();
                //}

                document.Close();
            }

            return ms;
        }

    }
}
