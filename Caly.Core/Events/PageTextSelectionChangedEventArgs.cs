using Avalonia.Input;
using Caly.Pdf.Models;
using System;
using Avalonia;

namespace Caly.Core.Events;

public abstract class PageInteractiveLayerPointerEventArgs : EventArgs
{
    protected PageInteractiveLayerPointerEventArgs(int pageNumber,
        Point position,
        PointerPointProperties properties,
        KeyModifiers keyModifiers,
        PdfWord? word,
        PdfAnnotation? annotation)
    {
        System.Diagnostics.Debug.Assert(pageNumber >= 1);
        PageNumber = pageNumber;
        Position = position;
        Properties = properties;
        KeyModifiers = keyModifiers;
        Word = word;
        Annotation = annotation;
    }

    public PointerPointProperties Properties { get; }

    public KeyModifiers KeyModifiers { get; }

    public Point Position { get; }

    public int PageNumber { get; }

    public PdfWord? Word { get; }

    public PdfAnnotation? Annotation { get; }

    public bool IsPanning()
    {
        if (!Properties.IsLeftButtonPressed)
        {
            return false;
        }

        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        return hotkeys is not null && KeyModifiers.HasFlag(hotkeys.CommandModifiers);
    }

    public bool IsPanningOrZooming()
    {
        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        return hotkeys is not null && KeyModifiers.HasFlag(hotkeys.CommandModifiers);
    }
}

public sealed class PageInteractiveLayerPointerExitedEventArgs : PageInteractiveLayerPointerEventArgs
{
    public PageInteractiveLayerPointerExitedEventArgs(int pageNumber, Point position,
        PointerPointProperties properties, KeyModifiers keyModifiers)
        : base(pageNumber, position, properties, keyModifiers, null, null)
    { }
}

public sealed class PageInteractiveLayerPointerMovedEventArgs : PageInteractiveLayerPointerEventArgs
{
    public PageInteractiveLayerPointerMovedEventArgs(int pageNumber,
        Point position,
        PointerPointProperties properties,
        KeyModifiers keyModifiers,
        PdfWord? word,
        PdfAnnotation? annotation,
        Point? startPosition)
        : base(pageNumber, position, properties, keyModifiers, word, annotation)
    {
        StartPosition = startPosition;
    }

    public Point? StartPosition { get; }

    public bool IsDragging => StartPosition.HasValue;
}

public sealed class PageInteractiveLayerPointerReleasedEventArgs : PageInteractiveLayerPointerEventArgs
{
    public PageInteractiveLayerPointerReleasedEventArgs(int pageNumber,
        Point position,
        PointerPointProperties properties,
        KeyModifiers keyModifiers,
        PdfWord? word,
        PdfAnnotation? annotation)
        : base(pageNumber, position, properties, keyModifiers, word, annotation)
    { }
}

public sealed class PageInteractiveLayerPointerPressedEventArgs : PageInteractiveLayerPointerEventArgs
{
    public PageInteractiveLayerPointerPressedEventArgs(int pageNumber,
        Point position,
        PointerPointProperties properties,
        KeyModifiers keyModifiers,
        int clickCount,
        PdfWord? word,
        PdfAnnotation? annotation)
        : base(pageNumber, position, properties, keyModifiers, word, annotation)
    {
        ClickCount = clickCount;
    }

    public int ClickCount { get; } 
}

public class PageTextSelectionChangedEventArgs : EventArgs
{
    public PageTextSelectionChangedEventArgs(int pageNumber, PdfWord? startWord, PdfWord? endWord,
        bool isMultipleClick = false)
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
    public PageTextSelectionResetEventArgs(int pageNumber)
        : base(pageNumber, null, null)
    { }
}
