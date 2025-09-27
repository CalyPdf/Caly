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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Caly.Core.Controls;
using Caly.Pdf.Models;

namespace Caly.Core.Handlers
{
    public sealed partial class PageInteractiveLayerHandler
    {
        private static void ShowAnnotation(PdfPageTextLayerControl control, PdfAnnotation annotation)
        {
            if (FlyoutBase.GetAttachedFlyout(control) is not Flyout attachedFlyout)
            {
                return;
            }

            // TODO - Should we use MVVM instead?
            var contentText = new Avalonia.Controls.TextBlock()
            {
                MaxWidth = 200,
                TextWrapping = TextWrapping.Wrap,
                Text = annotation.Content
            };

            if (!string.IsNullOrEmpty(annotation.Date))
            {
                attachedFlyout.Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock()
                        {
                            Text = annotation.Date
                        },
                        contentText
                    }
                };
            }
            else
            {
                attachedFlyout.Content = contentText;
            }

            attachedFlyout.ShowAt(control);
        }

        private static void HideAnnotation(PdfPageTextLayerControl control)
        {
            if (FlyoutBase.GetAttachedFlyout(control) is Flyout attachedFlyout)
            {
                attachedFlyout.Hide();
                attachedFlyout.Content = null;
            }
        }
    }
}
