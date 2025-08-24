namespace Caly.Printing.Models
{
    public sealed class CalyPrintJob
    {
        public required string PrinterName { get; init; }

        public required string DocumentName { get; init; }

        public IReadOnlyList<Range>? PagesRanges { get; init; }

        public PagesToPrint PagesToPrintType { get; init; }

        public int CopiesCount { get; init; }

        // Pages range

        // black & white / colors

        // number of copies
    }
}
