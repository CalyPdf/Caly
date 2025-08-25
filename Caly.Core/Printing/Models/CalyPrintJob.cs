using System;
using System.Collections.Generic;
using Caly.Core.ViewModels;

namespace Caly.Core.Printing.Models
{
    public sealed class CalyPrintJob
    {
        public required PdfDocumentViewModel PdfDocument { get; init; }

        public required string PrinterName { get; init; }

        public required string DocumentName { get; init; }

        public required IReadOnlyList<Range> PagesRanges { get; init; }

        public int CopiesCount { get; init; }

        // black & white / colors
    }
}
