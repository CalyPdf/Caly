using Caly.Printing.Models;
using Caly.Printing.Services.Interfaces;

namespace Caly.Printing.Services
{
    public sealed class NoOpCalyPrintingService : IPrintingService
    {
        public bool IsSupported => false;

        public void AddJob(CalyPrintJob printJob)
        {
            throw new InvalidOperationException("Cannot add printing job, printing is not supported");
        }

        public IEnumerable<CalyPrinterDevice> GetPrinters()
        {
            throw new InvalidOperationException("Cannot get printers, printing is not supported");
        }
    }
}
