using Caly.Printing.Models;

namespace Caly.Printing.Services.Interfaces
{
    public interface IPrintingService
    {
        bool IsSupported { get; }

        void AddJob(CalyPrintJob printJob);

        IEnumerable<CalyPrinterDevice> GetPrinters();
    }
}
