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
using System.IO;
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

    private static ReadOnlySpan<char> PdfExtension => ['.', 'p', 'd', 'f'];

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

    internal static void OpenFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !IsValidFilePath(path) || !File.Exists(path))
        {
            return;
        }

        if (path.IsPdf())
        {
            OpenPdfDocument(path);
            return;
        }

        // We don't want to directly open any other file extension
        // as this could be harmful. We just open the directory.
        var directory = Path.GetDirectoryName(path);
        OpenDirectory(directory);
    }

    private static bool IsValidFilePath(ReadOnlySpan<char> path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
        {
            return false;
        }

        var dir = Path.GetDirectoryName(path);
        if (!IsValidPath(dir))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidPath(ReadOnlySpan<char> path)
    {
        if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Open a pdf document with Caly.
    /// </summary>
    internal static void OpenPdfDocument(string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            if (!path.IsPdf())
            {
                return;
            }

            if (FilePipeStream.SendPath(path))
            {
                FilePipeStream.SendBringToFront();
            }
        }
        catch (Exception e)
        {
            Debug.WriteExceptionToFile(e);
        }
    }

    internal static void OpenDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path) || !IsValidPath(path) || !Directory.Exists(path))
        {
            return;
        }

        ProcessStart(path);
    }

    /// <summary>
    /// Open a uri.
    /// </summary>
    internal static void OpenUri(string? uri)
    {
        // https://en.wikipedia.org/wiki/Uniform_Resource_Identifier
        if (string.IsNullOrEmpty(uri))
        {
            return;
        }

        try
        {
            var index = uri.IndexOf(':');
            if (index == -1)
            {
                if (!uri.StartsWith("http"))
                {
                    // We only want to open http / https uris in this method.
                    // We force 'http'.
                    uri = $"http://{uri}";
                }
            }
            else
            {
                var scheme = uri.AsSpan(0, index);
                switch (scheme)
                {
                    case "ftp":
                    case "http":
                    case "https":
                    case "mailto":
                    case "tel":
                    case "imap":
                        // OK
                        break;

                    case "file": // TODO - Open directory?
                    default:
                        return;
                }
            }

            var uriObj = new Uri(uri);
            ProcessStart(uriObj.AbsoluteUri);
        }
        catch (Exception e)
        {
            Debug.WriteExceptionToFile(e);
        }
    }

    /// <summary>
    /// Open a url.
    /// </summary>
    internal static void OpenUri(ReadOnlySpan<char> url)
    {
        OpenUri(new string(url));
    }

    /// <summary>
    /// Open a link (local path, url, etc.).
    /// <para>Warning - sanitise the input as this will execute anything passed.</para>
    /// </summary>
    private static void ProcessStart(string url)
    {
        // https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/

        // See https://docs.avaloniaui.net/docs/concepts/services/launcher to use avalonia to do so

        try
        {
            Process.Start(url);
        }
        catch (Exception ex)
        {
            ProcessStartFallback(url);
        }
    }
    
    /// <summary>
    /// Warning - sanitise the input as this will execute anything passed.
    /// </summary>
    private static void ProcessStartFallback(string url)
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

    internal static bool IsPdf(this ReadOnlySpan<char> path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Length == 4)
        {
            return extension.Equals(PdfExtension, StringComparison.OrdinalIgnoreCase);
        }
        return false;
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
