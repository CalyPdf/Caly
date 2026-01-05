using System;

namespace Caly.Core.Events;

public sealed class PagesVisibilityChangedEventArgs : EventArgs
{
    public int FirstRealised { get; }

    public int FirstVisible { get; }

    public int LastRealised { get; }

    public int LastVisible { get; }

    public int? Current { get; }

    public PagesVisibilityChangedEventArgs(int firstVisible, int lastVisible, int firstRealised, int lastRealised, int? current)
    {
        FirstRealised = firstRealised;
        FirstVisible = firstVisible;
        LastRealised = lastRealised;
        LastVisible = lastVisible;
        Current = current;
    }

    public override bool Equals(object? obj)
    {
        if (obj is PagesVisibilityChangedEventArgs other)
        {
            return FirstRealised == other.FirstRealised &&
                   LastRealised == other.LastRealised &&
                   FirstVisible == other.FirstVisible &&
                   LastVisible == other.LastVisible &&
                   Current == other.Current;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FirstRealised, FirstVisible, LastRealised, LastVisible, Current);
    }
}
