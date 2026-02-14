using System;

namespace Caly.Core.Events;

public sealed class TextSelectionExtendedEventArgs : EventArgs
{
    public int AnchorPageIndex { get; init; } = -1;

    public int FocusPageIndex { get; init; } = -1;
}