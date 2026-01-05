using System;

namespace Caly.Core.Events;

public sealed class JumpedToPageEventArgs : EventArgs
{
    public int PageNumber { get; }

    public JumpedToPageEventArgs(int pageNumber)
    {
        PageNumber = pageNumber;
    }
}