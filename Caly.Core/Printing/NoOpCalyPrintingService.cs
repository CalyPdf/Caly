using System;
using System.Collections.Generic;
using Caly.Core.Printing.Models;
using Caly.Core.Printing.Services.Interfaces;

namespace Caly.Core.Printing
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
