using Avalonia.Data;
using Caly.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Caly.Printing.Models;

namespace Caly.Core.ViewModels
{
    public sealed partial class PrintersViewModel : ViewModelBase
    {
        private const char PageRangeSeparator = '-';

        [GeneratedRegex(@"\s*(\d+)(?:-(\d+))?\s*", RegexOptions.NonBacktracking, 5_000)]
        public static partial Regex CustomPagesMatch();

        public PagesToPrint[] PagesToPrintChoices => [PagesToPrint.All, PagesToPrint.Current, PagesToPrint.Custom];

        //public ObservableCollection<string> Printers { get; } = new();
        
        public int MaxCopies => 1000;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayCustomPages))]
        private int _selectedPagesIndex;

        [ObservableProperty]
        private CalyPrinterDevice? _selectedPrinterDevice;

        public bool DisplayCustomPages
        {
            get
            {
                CustomPages = string.Empty; // Reset
                return PagesToPrintChoices[SelectedPagesIndex] == PagesToPrint.Custom;
            }
        }

        private string? _customPages;
        public string? CustomPages
        {
            get => _customPages;
            set
            {
                SetProperty(ref _customPages, value);
                _ = GetPagesRanges(_customPages);
            }
        }

        private string? _copiesCountText = "1";
        public string? CopiesCountText
        {
            get => _copiesCountText;
            set
            {
                SetProperty(ref _copiesCountText, value);

                if (!int.TryParse(value, out int count))
                {
                    throw new DataValidationException("Invalid number of copies.");
                }

                if (count <= 0)
                {
                    throw new DataValidationException("Number of copies should be 1 or more.");
                }

                if (count > MaxCopies)
                {
                    throw new DataValidationException("Number of copies should less than 1000.");
                }
            }
        }

        public Task<ObservableCollection<CalyPrinterDevice>> PrintersAsync => GetPrinters();
        private async Task<ObservableCollection<CalyPrinterDevice>> GetPrinters()
        {
            var printers = await StrongReferenceMessenger.Default.Send(PrintersRequestMessage.Instance);
            if (printers.Count > 0)
            {
                SelectedPrinterDevice = printers[0];
            }
            return new ObservableCollection<CalyPrinterDevice>(printers);
        }

        private static IReadOnlyList<Range> GetPagesRanges(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return [];
            }

            var ranges = new List<Range>();

            ReadOnlySpan<char> valueSpan = value.AsSpan();
            if (!CustomPagesMatch().IsMatch(valueSpan))
            {
                throw new DataValidationException("Provide valid pages, e.g. 1-5, 15, 17, 25-30");
            }

            foreach (var match in CustomPagesMatch().EnumerateMatches(valueSpan))
            {
                var m = valueSpan.Slice(match.Index, match.Length);
                if (m.Contains(PageRangeSeparator))
                {
                    // We have a range
                    int? startPage = null;
                    int? endPage = null;
                    foreach (var page in m.Split(PageRangeSeparator))
                    {
                        var sValue = m[page];
                        if (!int.TryParse(sValue, out int v))
                        {
                            if (v <= 0)
                            {
                                throw new DataValidationException($"Page number '{v}' is not valid. It should be 1 or more.");
                            }
                        }

                        if (!startPage.HasValue && !endPage.HasValue)
                        {
                            startPage = v;
                        }
                        else if (!endPage.HasValue)
                        {
                            endPage = v;
                        }
                        else
                        {
                            throw new DataValidationException("Provide valid pages, e.g. 1-5, 15, 17, 25-30");
                        }
                    }

                    if (!startPage.HasValue || !endPage.HasValue)
                    {
                        throw new DataValidationException($"Invalid range '{m}'.");
                    }

                    if (startPage.Value > endPage.Value)
                    {
                        throw new DataValidationException($"Invalid range '{m}': Start page should be before end page.");
                    }

                    ranges.Add(new Range(startPage.Value, endPage.Value + 1));
                }
                else if (int.TryParse(m, out int page))
                {
                    if (match.Index > 0 && valueSpan[match.Index - 1] == PageRangeSeparator)
                    {
                        throw new DataValidationException($"Page number '-{m}' is not valid. It should be 1 or more.");
                    }

                    if (page <= 0)
                    {
                        throw new DataValidationException($"Page number '{m}' is not valid. It should be 1 or more.");
                    }

                    ranges.Add(new Range(page, page + 1));
                }
                else
                {
                    throw new DataValidationException($"Invalid page '{m}'.");
                }
            }

            return ranges;
        }
        
        [RelayCommand]
        private void Print(PdfDocumentViewModel? pdfDocument)
        {
            ArgumentNullException.ThrowIfNull(pdfDocument, nameof(pdfDocument));
            ArgumentNullException.ThrowIfNull(SelectedPrinterDevice, nameof(SelectedPrinterDevice));

            var pagesToPrintType = PagesToPrintChoices[SelectedPagesIndex];
            IReadOnlyList<Range>? pagesRanges = null;
            switch (pagesToPrintType)
            {
                case PagesToPrint.Custom:
                    pagesRanges = GetPagesRanges(CustomPages);
                    break;

                case PagesToPrint.Current:
                    int selectedPage = pdfDocument.SelectedPageIndex ?? throw new Exception("Invalid selected page");
                    pagesRanges = [new Range(selectedPage, selectedPage + 1)];
                    break;

                case PagesToPrint.All:
                    pagesRanges = [new Range(1, pdfDocument.PageCount + 1)];
                    break;
            }

            if (!int.TryParse(CopiesCountText, out int copiesCount))
            {
                // error
            }

            var job = new CalyPrintJob()
            {
                DocumentName = pdfDocument.FileName!,
                PrinterName = SelectedPrinterDevice.Name,
                PagesRanges = pagesRanges,
                CopiesCount = copiesCount
            };

            var success = StrongReferenceMessenger.Default.Send(new PrintDocumentRequestMessage()
            {
                PrintingJob = job
            });

            if (!success)
            {
                // error
            }
        }

        internal enum PagesToPrint : byte
        {
            All = 0,
            Current = 1,
            Custom = 2
        }
    }
}
