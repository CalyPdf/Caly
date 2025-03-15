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

namespace Caly.Pdf.Models
{
    public sealed class PdfDocumentProperties
    {
        /// <summary>
        /// The Pdf version.
        /// </summary>
        public required string PdfVersion { get; init; }

        /// <summary>
        /// The title of this document if applicable.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// The name of the person who created this document if applicable.
        /// </summary>
        public string? Author { get; init; }

        /// <summary>
        /// The subject of this document if applicable.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Any keywords associated with this document if applicable.
        /// </summary>
        public string? Keywords { get; init; }

        /// <summary>
        /// The name of the application which created the original document before it was converted to PDF if applicable.
        /// </summary>
        public string? Creator { get; init; }

        /// <summary>
        /// The name of the application used to convert the original document to PDF if applicable.
        /// </summary>
        public string? Producer { get; init; }

        /// <summary>
        /// The date and time the document was created.
        /// </summary>
        public string? CreationDate { get; init; }

        /// <summary>
        /// The date and time the document was most recently modified.
        /// </summary>
        public string? ModifiedDate { get; init; }

        /// <summary>
        /// Other information.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Others { get; init; }
    }
}
