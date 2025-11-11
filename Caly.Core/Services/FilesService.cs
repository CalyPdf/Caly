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
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services
{
    // https://github.com/AvaloniaUI/AvaloniaUI.QuickGuides/blob/main/IoCFileOps/Services/FilesService.cs

    internal sealed class FilesService : IFilesService
    {
        private readonly IStorageProvider _storageProvider;
        private readonly IReadOnlyList<FilePickerFileType> _pdfFileFilter = [FilePickerFileTypes.Pdf];

        public FilesService(IStorageProvider? storageProvider)
        {
#if DEBUG
            if (Avalonia.Controls.Design.IsDesignMode)
            {
                _storageProvider = storageProvider!;
                return;
            }
#endif
            _storageProvider = storageProvider ?? throw new ArgumentNullException($"Could not find {typeof(IStorageProvider)}."); ;
        }

        public async Task<IStorageFile?> OpenPdfFileAsync()
        {
            Debug.ThrowNotOnUiThread();

            IReadOnlyList<IStorageFile> files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open",
                AllowMultiple = false,
                FileTypeFilter = _pdfFileFilter
            });

            return files.Count >= 1 ? files[0] : null;
        }

        public Task<IStorageFile?> SavePdfFileAsync()
        {
            return _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Save Pdf File"
            });
        }

        public async Task<IStorageFile?> TryGetFileFromPathAsync(string path)
        {
            try
            {
                // UIThread needed for Avalonia.FreeDesktop.DBusSystemDialog
                return await Dispatcher.UIThread.InvokeAsync(() => _storageProvider.TryGetFileFromPathAsync(path));
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Could not get TopLevel in FilesService.TryGetFileFromPathAsync (path: '{path}').");
                Debug.WriteExceptionToFile(e);
                return null;
            }
        }
    }
}
