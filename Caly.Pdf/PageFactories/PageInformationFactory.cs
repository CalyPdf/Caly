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

using Caly.Pdf.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Geometry;
using UglyToad.PdfPig.Logging;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Parser;
using UglyToad.PdfPig.Parser.Parts;
using UglyToad.PdfPig.Tokenization.Scanner;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.Util;

namespace Caly.Pdf.PageFactories
{
    public sealed class PageInformationFactory : IPageFactory<PdfPageInformation>
    {
        /// <summary>
        /// The parsing options.
        /// </summary>
        private readonly ParsingOptions _parsingOptions;

        /// <summary>
        /// The Pdf token scanner.
        /// </summary>
        private readonly IPdfTokenScanner _pdfScanner;

        /// <summary>
        /// Create a <see cref="BasePageFactory{TPage}"/>.
        /// </summary>
        public PageInformationFactory(
            IPdfTokenScanner pdfScanner,
            IResourceStore resourceStore,
            ILookupFilterProvider filterProvider,
            IPageContentParser pageContentParser,
            ParsingOptions parsingOptions)
        {
            _pdfScanner = pdfScanner;
            _parsingOptions = parsingOptions;
        }

        /// <inheritdoc/>
        public PdfPageInformation Create(int number, DictionaryToken dictionary, PageTreeMembers pageTreeMembers, NamedDestinations namedDestinations)
        {
            if (dictionary is null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            NameToken? type = dictionary.GetNameOrDefault(NameToken.Type);

            if (type is not null && !type.Equals(NameToken.Page))
            {
                _parsingOptions.Logger.Error($"Page {number} had its type specified as {type} rather than 'Page'.");
            }

            var rotation = new PageRotationDegrees(pageTreeMembers.Rotation);
            if (dictionary.TryGet(NameToken.Rotate, _pdfScanner, out NumericToken? rotateToken))
            {
                rotation = new PageRotationDegrees(rotateToken.Int);
            }

            MediaBox mediaBox = GetMediaBox(number, dictionary, pageTreeMembers);
            CropBox cropBox = GetCropBox(dictionary, mediaBox);

            TransformationMatrix initialMatrix = GetInitialMatrix(GetUserSpaceUnits(dictionary), mediaBox, cropBox, rotation, _parsingOptions.Logger);

            ApplyTransformNormalise(initialMatrix, ref mediaBox, ref cropBox);

            // Special case where cropbox is outside mediabox: use cropbox instead of intersection
            var effectiveCropBox = mediaBox.Bounds.Intersect(cropBox.Bounds) ?? cropBox.Bounds;

            return new PdfPageInformation()
            {
                PageNumber = number,
                Width = effectiveCropBox.Width,
                Height = effectiveCropBox.Height
            };
        }

        /// <summary>
        /// Get the user space units.
        /// </summary>
        private static int GetUserSpaceUnits(DictionaryToken dictionary)
        {
            if (dictionary.TryGet(NameToken.UserUnit, out IToken? userUnitBase) && userUnitBase is NumericToken userUnitNumber)
            {
                return userUnitNumber.Int;
            }

            return UserSpaceUnit.Default.PointMultiples;
        }

        /// <summary>
        /// Get the crop box.
        /// </summary>
        private CropBox GetCropBox(DictionaryToken dictionary, MediaBox mediaBox)
        {
            if (dictionary.TryGet(NameToken.CropBox, out var cropBoxObject) &&
                DirectObjectFinder.TryGet(cropBoxObject, _pdfScanner, out ArrayToken? cropBoxArray))
            {
                if (cropBoxArray.Length == 4)
                {
                    return new CropBox(cropBoxArray.ToRectangle(_pdfScanner));
                }

                _parsingOptions.Logger.Error(
                    $"The CropBox was the wrong length in the dictionary: {dictionary}. Array was: {cropBoxArray}. Using MediaBox.");

                return new CropBox(mediaBox.Bounds);
            }

            return new CropBox(mediaBox.Bounds);
        }

        /// <summary>
        /// Get the media box.
        /// </summary>
        private MediaBox GetMediaBox(int number, DictionaryToken dictionary, PageTreeMembers pageTreeMembers)
        {
            if (dictionary.TryGet(NameToken.MediaBox, out var mediaBoxObject)
                && DirectObjectFinder.TryGet(mediaBoxObject, _pdfScanner, out ArrayToken? mediaBoxArray))
            {
                if (mediaBoxArray.Length == 4)
                {
                    return new MediaBox(mediaBoxArray.ToRectangle(_pdfScanner));
                }

                _parsingOptions.Logger.Error(
                    $"The MediaBox was the wrong length in the dictionary: {dictionary}. Array was: {mediaBoxArray}. Defaulting to US Letter.");

                return MediaBox.Letter;
            }

            MediaBox mediaBox = pageTreeMembers.MediaBox;

            if (mediaBox is not null)
            {
                return mediaBox;
            }

            _parsingOptions.Logger.Error($"The MediaBox was the wrong missing for page {number}. Using US Letter.");

            // PDFBox defaults to US Letter.
            return MediaBox.Letter;
        }

        /// <summary>
        /// Apply the matrix transform to the media box and crop box.
        /// Then Normalise() in order to obtain rectangles with rotation=0
        /// and width and height as viewed on screen.
        /// </summary>
        private static void ApplyTransformNormalise(TransformationMatrix transformationMatrix, ref MediaBox mediaBox, ref CropBox cropBox)
        {
            if (transformationMatrix == TransformationMatrix.Identity) return;
            mediaBox = new MediaBox(transformationMatrix.Transform(mediaBox.Bounds).Normalise());
            cropBox = new CropBox(transformationMatrix.Transform(cropBox.Bounds).Normalise());
        }

        // From OperationContextHelper
        /// <summary>
        /// Get the initial transformation matrix.
        /// </summary>
        /// <param name="userSpaceUnit">User space unit.</param>
        /// <param name="mediaBox">The Media box as define in the document, without any applied transform.</param>
        /// <param name="cropBox">The Crop box as define in the document, without any applied transform.</param>
        /// <param name="rotation">The page rotation.</param>
        /// <param name="log"></param>
        [System.Diagnostics.Contracts.Pure]
        private static TransformationMatrix GetInitialMatrix(int userSpaceUnit,
            MediaBox mediaBox,
            CropBox cropBox,
            PageRotationDegrees rotation,
            ILog log)
        {
            // Cater for scenario where the cropbox is larger than the mediabox.
            // If there is no intersection (method returns null), fall back to the cropbox.
            var viewBox = mediaBox.Bounds.Intersect(cropBox.Bounds) ?? cropBox.Bounds;

            if (rotation.Value == 0
                && viewBox.Left == 0
                && viewBox.Bottom == 0
                && userSpaceUnit == 1)
            {
                return TransformationMatrix.Identity;
            }

            // Move points so that (0,0) is equal to the viewbox bottom left corner.
            var t1 = TransformationMatrix.GetTranslationMatrix(-viewBox.Left, -viewBox.Bottom);

            if (userSpaceUnit != 1)
            {
                log.Warn("User space unit other than 1 is not implemented");
            }

            // After rotating around the origin, our points will have negative x/y coordinates.
            // Fix this by translating them by a certain dx/dy after rotation based on the viewbox.
            double dx, dy;
            switch (rotation.Value)
            {
                case 0:
                    // No need to rotate / translate after rotation, just return the initial
                    // translation matrix.
                    return t1;
                case 90:
                    // Move rotated points up by our (unrotated) viewbox width
                    dx = 0;
                    dy = viewBox.Width;
                    break;
                case 180:
                    // Move rotated points up/right using the (unrotated) viewbox width/height
                    dx = viewBox.Width;
                    dy = viewBox.Height;
                    break;
                case 270:
                    // Move rotated points right using the (unrotated) viewbox height
                    dx = viewBox.Height;
                    dy = 0;
                    break;
                default:
                    throw new InvalidOperationException($"Invalid value for page rotation: {rotation.Value}.");
            }

            // GetRotationMatrix uses counter clockwise angles, whereas our page rotation
            // is a clockwise angle, so flip the sign.
            var r = TransformationMatrix.GetRotationMatrix(-rotation.Value);

            // Fix up negative coordinates after rotation
            var t2 = TransformationMatrix.GetTranslationMatrix(dx, dy);

            // Now get the final combined matrix T1 > R > T2
            return t1.Multiply(r.Multiply(t2));
        }
    }
}
