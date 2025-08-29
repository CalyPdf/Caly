using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.Services
{
    public abstract class BaseClipboardService : IClipboardService
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

        public static bool ShouldDehyphenate(in UnicodeCategory prevCategory, in int previousLine, in int currentLine)
        {
            // This is a very basic dehyphenation method
            return currentLine > previousLine && prevCategory is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation;
        }

        private static bool ShouldAddWhitespace(in UnicodeCategory prevCategory, in UnicodeCategory currentCategory)
        {
            return !_noSpaceAfter.Contains(prevCategory) &&
                   !_noSpaceBefore.Contains(currentCategory);
        }

        public async Task<bool> SetAsync(PdfDocumentViewModel document, CancellationToken token)
        {
            // TODO - Check use of tasks here

            ArgumentNullException.ThrowIfNull(document.TextSelectionHandler, nameof(document.TextSelectionHandler));

            PdfTextSelection selection = document.TextSelectionHandler.Selection;

            if (!selection.IsValid)
            {
                return false;
            }

            // https://docs.avaloniaui.net/docs/next/concepts/services/clipboardS

            System.Diagnostics.Debug.WriteLine("Starting IClipboardService.SetAsync");

            string text = await Task.Run(async () =>
            {
                var request = selection.GetDocumentSelectionAsAsync(
                    GetWord, GetPartialWord,
                    document, token);

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

                    var sb = new StringBuilder().Append(enumerator.Current.Word);
                    int previousLine = enumerator.Current.LineNumber;

                    while (await enumerator.MoveNextAsync())
                    {
                        var blob = enumerator.Current;

                        if (blob.Word.AsSpan().IsEmpty)
                        {
                            continue;
                        }

                        var prevLast = CharUnicodeInfo.GetUnicodeCategory(sb[^1]);

                        if (ShouldDehyphenate(in prevLast, in previousLine, blob.LineNumber))
                        {
                            sb.Length--; // Remove hyphen
                        }
                        else if (ShouldAddWhitespace(in prevLast, CharUnicodeInfo.GetUnicodeCategory(blob.Word.AsSpan()[0])))
                        {
                            sb.Append(' '); // TODO - Add condition to check if next word exist
                        }

                        sb.Append(blob.Word);
                        previousLine = blob.LineNumber;
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

            return true;
        }

        public abstract Task SetAsync(string text);

        public abstract Task<string?> GetAsync();

        public abstract Task ClearAsync();

        private readonly struct TextBlob
        {
            public TextBlob(string word, int lineNumber)
            {
                Word = word;
                LineNumber = lineNumber;
            }

            public string Word { get; }

            public int LineNumber { get; }

            public bool IsEmpty => Word.AsSpan().IsEmpty;
        }

        private static TextBlob GetWord(PdfWord word)
        {
            return new TextBlob(word.Value, word.TextLineIndex);
        }

        private static TextBlob GetPartialWord(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex != -1);
            System.Diagnostics.Debug.Assert(endIndex != -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            endIndex = word.GetCharIndexFromBboxIndex(endIndex);

            var span = word.Value.AsSpan().Slice(startIndex, endIndex - startIndex + 1);

            return new TextBlob(span.ToString(), word.TextLineIndex);
        }
    }
}
