using System;
using Caly.Pdf.Models;

namespace Caly.Core.Events
{
    public class PageTextSelectionChangedEventArgs : EventArgs
    {
        public PageTextSelectionChangedEventArgs(int pageNumber, PdfWord? startWord, PdfWord? endWord, bool isMultipleClick = false)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
            
            PageNumber = pageNumber;
            StartWord = startWord;
            EndWord = endWord;
            IsMultipleClick = isMultipleClick;
        }

        public int PageNumber { get; }

        public PdfWord? StartWord { get; }

        public PdfWord? EndWord { get; }

        public bool IsMultipleClick { get; }

        public bool IsReset => StartWord is null && EndWord is null;
    }

    public sealed class PageTextSelectionResetEventArgs : PageTextSelectionChangedEventArgs
    {
        public PageTextSelectionResetEventArgs(int pageNumber) : base(pageNumber, null, null)
        { }
    }
}
