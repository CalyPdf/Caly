using System;

namespace Caly.Core.Events;

public sealed class TextSelectionFocusPageChangedEventArgs : EventArgs
{
    public int OldFocusPageIndex { get; init; } = -1;

    public int NewFocusPageIndex { get; init; } = -1;
}
