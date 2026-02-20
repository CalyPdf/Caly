// Copyright (c) 2025 BobLd
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Caly.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using UglyToad.PdfPig.Core;

namespace Caly.Core.Utilities;

internal static class CalyExtensions
{
    public static readonly string CalyVersion;

    static CalyExtensions()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        string? version = assembly.GetName().Version?.ToString().Trim();
        CalyVersion = !string.IsNullOrEmpty(version) ? version : @"n/a";
    }

    public static bool IsMobilePlatform()
    {
        return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
    }

    /// <summary>
    /// Get the Viewport Rect to check if elements are visible or not.
    /// </summary>
    public static Rect GetViewportRect(this ScrollViewer sv)
    {
        return new Rect(sv.Offset.X, sv.Offset.Y, sv.Viewport.Width, sv.Viewport.Height);
    }

    public static T FindFromNameScope<T>(this INameScope e, string name) where T : Control
    {
        var element = e.Find<T>(name);
        return element ?? throw new NullReferenceException($"Could not find {name}.");
    }

    public static bool IsEmpty(this Rect rect)
    {
        return rect.Size.IsEmpty();
    }

    public static bool IsEmpty(this Size size)
    {
        return size.Height <= float.Epsilon || size.Width <= float.Epsilon;
    }

    public static PdfRectangle ToPdfRectangle(this Rect rect)
    {
        return new PdfRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top);
    }

    public static bool IsPanning(this PointerEventArgs e)
    {
        if (!e.Properties.IsLeftButtonPressed)
        {
            return false;
        }

        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        return hotkeys is not null && e.KeyModifiers.HasFlag(hotkeys.CommandModifiers);
    }

    public static bool IsPanningOrZooming(this PointerEventArgs e)
    {
        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        return hotkeys is not null && e.KeyModifiers.HasFlag(hotkeys.CommandModifiers);
    }

    public static bool IsPanningOrZooming(this KeyEventArgs e)
    {
        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        return hotkeys is not null && e.KeyModifiers.HasFlag(hotkeys.CommandModifiers);
    }

    /// <summary>
    /// Thread safe.
    /// </summary>
    public static void AddSafely<T>(this ObservableCollection<T> collection, T element)
    {
        IList list = collection;
        lock (list.SyncRoot)
        {
            list.Add(element);
        }
    }

    /// <summary>
    /// Thread safe.
    /// </summary>
    public static void AddSortedSafely<T>(this SortedObservableCollection<T> collection, T element)
    {
        IList list = collection;
        lock (list.SyncRoot)
        {
            collection.AddSorted(element);
        }
    }

    /// <summary>
    /// Thread safe.
    /// </summary>
    public static void ClearSafely<T>(this ObservableCollection<T> collection)
    {
        IList list = collection;
        lock (list.SyncRoot)
        {
            list.Clear();
        }
    }

    /// <summary>
    /// Thread safe.
    /// </summary>
    public static void RemoveSafely<T>(this ObservableCollection<T> collection, T element)
    {
        IList list = collection;
        lock (list.SyncRoot)
        {
            list.Remove(element);
        }
    }

    /// <summary>
    /// Thread safe.
    /// </summary>
    public static int IndexOfSafely<T>(this ObservableCollection<T> collection, T element)
    {
        IList list = collection;
        lock (list.SyncRoot)
        {
            return list.IndexOf(element);
        }
    }

    /// <summary>
    /// Open a link (local path, url, etc.).
    /// </summary>
    internal static void OpenLink(ReadOnlySpan<char> url)
    {
        OpenLink(new string(url));
    }

    /// <summary>
    /// Open a link (local path, url, etc.).
    /// </summary>
    internal static void OpenLink(string url)
    {
        // https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/

        try
        {
            Process.Start(url);
        }
        catch (Exception ex)
        {
            OpenLinkFallback(url);
        }
    }
    
    private static void OpenLinkFallback(string url)
    {
        try
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteExceptionToFile(ex);
            App.Messenger.Send(new ShowNotificationMessage(NotificationType.Error,
                $"Failed to open '{url}'.",
                ex.Message));
        }
    }

    /// <summary>
    /// The Euclidean distance is the "ordinary" straight-line distance between two points.
    /// </summary>
    /// <param name="point1">The first point.</param>
    /// <param name="point2">The second point.</param>
    public static double Euclidean(this Point point1, Point point2)
    {
        double dx = point1.X - point2.X;
        double dy = point1.Y - point2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
