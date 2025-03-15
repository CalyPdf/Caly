﻿// Copyright (c) 2025 BobLd
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
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.Services
{
    internal partial class PdfPigPdfService
    {
        // PdfPig only allow to read 1 page at a time for now
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private async Task<T?> ExecuteWithLockAsync<T>(Func<T> action, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (IsDisposed())
            {
                return default;
            }

            bool hasLock = false;
            try
            {
                await _semaphore.WaitAsync(token);
                hasLock = true;

                if (IsDisposed())
                {
                    return default;
                }

                token.ThrowIfCancellationRequested();
                return action();
            }
            finally
            {
                if (hasLock && !IsDisposed())
                {
                    _semaphore.Release();
                }
            }
        }

    }
}
