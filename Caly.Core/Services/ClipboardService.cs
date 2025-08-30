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
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace Caly.Core.Services
{
    internal sealed class ClipboardService : BaseClipboardService
    {
        private readonly IClipboard _clipboard;

        public ClipboardService(IClipboard clipboard)
        {
            _clipboard = clipboard;
            if (_clipboard is null)
            {
                throw new ArgumentNullException($"Could not find {typeof(IClipboard)}.");
            }
        }

        public override async Task SetAsync(string text)
        {
            await _clipboard.SetTextAsync(text);
        }

        public override Task<string?> GetAsync()
        {
            return _clipboard.GetTextAsync();
        }

        public override async Task ClearAsync()
        {
            await _clipboard.ClearAsync();
        }
    }
}
