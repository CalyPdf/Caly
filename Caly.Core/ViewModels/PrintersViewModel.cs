using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Caly.Core.ViewModels
{
    public sealed partial class PrintersViewModel : ViewModelBase
    {

        [GeneratedRegex(@"\s*(\d+)(?:-(\d+))?\s*", RegexOptions.NonBacktracking, 5_000)]
        public static partial Regex CustomPagesMatch();

        public PagesToPrint[] PagesToPrintChoices => [PagesToPrint.All, PagesToPrint.Current, PagesToPrint.Custom];

        public ObservableCollection<string> Printers { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayCustomPages))]
        private int _selectedPagesIndex;

        public int MaxCopies => 1000;

        public bool DisplayCustomPages
        {
            get
            {
                CustomPages = string.Empty; // Reset
                return PagesToPrintChoices[SelectedPagesIndex] == PagesToPrint.Custom;
            }
        }

        private string? _copiesCountText = "1";
        public string CopiesCountText
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

                if (count > MaxCopies) // Same as Max in xaml
                {
                    throw new DataValidationException("Number of copies should less than 1000.");
                }
            }
        }

        private string? _customPages;
        public string? CustomPages
        {
            get => _customPages;
            set
            {
                SetProperty(ref _customPages, value);
                _ = ValidateCustomPage(_customPages);
            }
        }

        private const char PageRangeSeparator = '-';

        private static IReadOnlyList<Range> ValidateCustomPage(string? value)
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

        [ObservableProperty] private int _selectedPrinterIndex;

        public PrintersViewModel()
        {
            for (int i = 0; i < 5; i++)
            {
                Printers.Add($"Printer #{i + 1}");
            }
        }

        [RelayCommand]
        private void Print()
        {

        }
    }

    public enum PagesToPrint : byte
    {
        All = 0,
        Current = 1,
        Custom = 2
    }
}
