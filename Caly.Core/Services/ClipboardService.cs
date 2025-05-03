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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;

namespace Caly.Core.Services
{
    internal sealed class ClipboardService : IClipboardService
    {
        // English rules
        private static readonly FrozenSet<UnicodeCategory> _noSpaceAfter = new HashSet<UnicodeCategory>
        {
            UnicodeCategory.OpenPunctuation,        // ( [ {
            UnicodeCategory.InitialQuotePunctuation,// “ ‘
            UnicodeCategory.DashPunctuation,        // -
            UnicodeCategory.ConnectorPunctuation    // _
        }.ToFrozenSet();

        private static readonly FrozenSet<UnicodeCategory> _noSpaceBefore = new HashSet<UnicodeCategory>
        {
            UnicodeCategory.ClosePunctuation,       // ) ] }
            UnicodeCategory.FinalQuotePunctuation,  // ” ’
            UnicodeCategory.OtherPunctuation,       // , . ! ?
            UnicodeCategory.DashPunctuation,        // -
            UnicodeCategory.MathSymbol,             // + = 
            UnicodeCategory.CurrencySymbol          // $
        }.ToFrozenSet();

        private readonly Visual _target;

        public ClipboardService(Visual target)
        {
            _target = target;
        }

        private static bool ShouldAddWhitespace(UnicodeCategory prevCategory, UnicodeCategory currentCategory)
        {
            return !_noSpaceAfter.Contains(prevCategory) &&
                   !_noSpaceBefore.Contains(currentCategory);
        }

        public async Task SetAsync(PdfDocumentViewModel document, CancellationToken token)
        {
            // TODO - Check use of tasks here

            PdfTextSelection selection = document.TextSelectionHandler.Selection;

            if (!selection.IsValid)
            {
                return;
            }
            
            // https://docs.avaloniaui.net/docs/next/concepts/services/clipboardS

            System.Diagnostics.Debug.WriteLine("Starting IClipboardService.SetAsync");

            string text = await Task.Run(async () =>
            {
                var request = selection.GetDocumentSelectionAsAsync(
                    w => w.Value,
                    PartialWord, document,
                    token);

                await using (var enumerator = request.GetAsyncEnumerator(token))
                {
                    while (enumerator.Current.IsEmpty && await enumerator.MoveNextAsync())
                    {
                        // Find first word that is not empty
                    }

                    if (enumerator.Current.IsEmpty)
                    {
                        return string.Empty;
                    }

                    var sb = new StringBuilder().Append(enumerator.Current);

                    while (await enumerator.MoveNextAsync())
                    {
                        var word = enumerator.Current;

                        if (word.IsEmpty)
                        {
                            continue;
                        }

                        var prevLast = CharUnicodeInfo.GetUnicodeCategory(sb[^1]);
                        var currentFirst = CharUnicodeInfo.GetUnicodeCategory(word.Span[0]);

                        if (ShouldAddWhitespace(prevLast, currentFirst))
                        {
                            sb.Append(' '); // TODO - Add condition to check if next word exist
                        }

                        sb.Append(word);
                    }

                    if (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
                    {
                        sb.Length--; // Last char added was a space
                    }

                    return sb.ToString();
                }
            }, token);

            await SetAsync(text);
            
            System.Diagnostics.Debug.WriteLine("Ended IClipboardService.SetAsync");
        }

        public async Task SetAsync(string text)
        {
            IClipboard clipboard = TopLevel.GetTopLevel(_target)?.Clipboard ??
                                   throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");

            await clipboard.SetTextAsync(text);
        }

        public async Task ClearAsync()
        {
            IClipboard clipboard = TopLevel.GetTopLevel(_target)?.Clipboard ??
                                   throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");

            await clipboard.ClearAsync();
        }
        
        private static ReadOnlyMemory<char> PartialWord(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex != -1);
            System.Diagnostics.Debug.Assert(endIndex != -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            endIndex = word.GetCharIndexFromBboxIndex(endIndex);

            return word.Value.Slice(startIndex, endIndex - startIndex + 1);
        }
    }
}
