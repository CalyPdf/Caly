using System;

namespace Caly.Core.Events
{
    public class PageTextSelectionChangedEventArgs : EventArgs
    {
        public PageTextSelectionChangedEventArgs(int pageNumber, int? startIndex, int? endIndex)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);

            if (startIndex.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(startIndex.Value, 0);
            }

            if (endIndex.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(endIndex.Value, 0);
            }

            PageNumber = pageNumber;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public int PageNumber { get; }

        public int? StartIndex { get; }

        public int? EndIndex { get; }

        public bool IsReset => !StartIndex.HasValue && !EndIndex.HasValue;
    }

    public sealed class PageTextSelectionResetEventArgs : PageTextSelectionChangedEventArgs
    {
        public PageTextSelectionResetEventArgs(int pageNumber) : base(pageNumber, null, null)
        { }
    }
}
