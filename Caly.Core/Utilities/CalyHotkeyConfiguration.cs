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
using System.Linq;
using Avalonia;
using Avalonia.Input;

namespace Caly.Core.Utilities
{
    internal static class CalyHotkeyConfiguration
    {
        private static readonly KeyModifiers CommandModifiers;

        static CalyHotkeyConfiguration()
        {
            if (Application.Current?.PlatformSettings is null)
            {
                throw new NullReferenceException("PlatformSettings is null.");
            }

            CommandModifiers = Application.Current.PlatformSettings.HotkeyConfiguration.CommandModifiers;
        }

        /// <summary>
        /// Gets a platform-specific <see cref="KeyGesture"/> for the Copy action
        /// </summary>
        public static KeyGesture? CopyGesture => Application.Current?.PlatformSettings?.HotkeyConfiguration.Copy.FirstOrDefault();

        public static KeyGesture OpenFileGesture => new KeyGesture(Key.O, CommandModifiers);

        public static KeyGesture DocumentSearchGesture => new KeyGesture(Key.F, CommandModifiers);

        public static KeyGesture DocumentCloseGesture => new KeyGesture(Key.F4, CommandModifiers);

        public static KeyGesture DocumentNextGesture => new KeyGesture(Key.PageDown, CommandModifiers);

        public static KeyGesture DocumentPreviousGesture => new KeyGesture(Key.PageUp, CommandModifiers);

        // TODO

        // See:
        // - MainView.axaml
        // - PageItem.axaml
        // - PageItemsControl.axaml
    }
}
