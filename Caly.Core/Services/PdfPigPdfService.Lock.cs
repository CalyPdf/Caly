// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
