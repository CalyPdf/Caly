using System.Collections.Generic;
using Caly.Core.Printing.Models;

namespace Caly.Core.Printing.Services.Interfaces
{
    public interface IPrintingService
    {
        bool IsSupported { get; }

        bool AddJob(CalyPrintJob printJob);

        IEnumerable<CalyPrinterDevice> GetPrinters();
    }
}
