using Caly.Printing.Models;
using Caly.Printing.Services.Interfaces;

namespace Caly.Printing.Services
{
    public sealed class NoOpCalyPrintingService : IPrintingService
    {
        public bool IsSupported => false;

        public bool AddJob(CalyPrintJob printJob)
        {
            return false;
        }

        public IEnumerable<CalyPrinterDevice> GetPrinters()
        {
            throw new InvalidOperationException("Cannot get printers, printing is not supported");
        }
    }
}
