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
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace Caly.Core.Utilities
{
    internal static class CalyExtensions
    {
        private static readonly int[] _versionParts = new int[4];

        static CalyExtensions()
        {
            var assemblyName = Process.GetCurrentProcess().MainModule.FileName;
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assemblyName);

            _versionParts[0] = fvi.ProductMajorPart;
            _versionParts[1] = fvi.ProductMinorPart;
            _versionParts[2] = fvi.ProductBuildPart;
            _versionParts[3] = fvi.ProductPrivatePart;
        }

        public static string GetCalyVersion()
        {
            return string.Join('.', _versionParts);
        }

        public static bool IsMobilePlatform()
        {
            return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
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

        /// <summary>
        /// Thread safe.
        /// </summary>
        public static void AddSafely<T>(this ObservableCollection<T> collection, T element)
        {
            var list = (IList)collection;
            lock (list.SyncRoot)
            {
                list.Add(element);
            }
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public static void ClearSafely<T>(this ObservableCollection<T> collection)
        {
            var list = (IList)collection;
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
            var list = (IList)collection;
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
            var list = (IList)collection;
            lock (list.SyncRoot)
            {
                return list.IndexOf(element);
            }
        }

        internal static void OpenBrowser(ReadOnlySpan<char> url)
        {
            OpenBrowser(new string(url));
        }

        internal static void OpenBrowser(string url)
        {
            // https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/

            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
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

        /*
        /// <summary>
        /// Transforms the rectangle by a matrix. Only uses doubles (and not floats contrary to Avalonia's) in order to not lose precision.
        /// </summary>
        /// <param name="rect">The rectangle to transform.</param>
        /// <param name="matrix">The transform.</param>
        /// <returns>The transformed rectangle.</returns>
        public static Rect TransformSafe(this Rect rect, Matrix matrix)
        {
            if (matrix.IsIdentity)
            {
                return rect;
            }

            // We can't use Avalonia's transforms as it will convert the doubles to floats
            return new Rect(rect.TopLeft.TransformSafe(matrix), rect.BottomRight.TransformSafe(matrix));
        }

        /// <summary>
        /// Transforms the point by a matrix. Only uses doubles (and not floats contrary to Avalonia's) in order to not lose precision.
        /// </summary>
        /// <param name="point">The point to transform.</param>
        /// <param name="matrix">The transform.</param>
        /// <returns>The transformed point.</returns>
        public static Point TransformSafe(this Point point, Matrix matrix)
        {
            if (matrix.IsIdentity)
            {
                return point;
            }

            // We can't use Avalonia's transforms as it will convert the doubles to floats
            return new Point(
                (point.X * matrix.M11) + (point.Y * matrix.M21),
                (point.X * matrix.M12) + (point.Y * matrix.M22));
        }

        /// <summary>
        /// Transforms the vector by a matrix. Only uses doubles (and not floats contrary to Avalonia's) in order to not lose precision.
        /// </summary>
        /// <param name="vector">The vector to transform.</param>
        /// <param name="matrix">The transform.</param>
        /// <returns>The transformed vector.</returns>
        public static Vector TransformSafe(this Vector vector, Matrix matrix)
        {
            if (matrix.IsIdentity)
            {
                return vector;
            }

            return new Vector(
                (vector.X * matrix.M11) + (vector.Y * matrix.M21),
                (vector.X * matrix.M12) + (vector.Y * matrix.M22));
        }
        */
    }
}
