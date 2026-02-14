using System;

namespace Caly.Core.Events;

public sealed class TextSelectionStartedEventArgs : EventArgs
{
    public int AnchorPageIndex { get; init; } = -1;
}