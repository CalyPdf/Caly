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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

[TemplatePart("PART_TextBoxSearch", typeof(TextBox))]
public sealed class PdfSearchPanelControl : TemplatedControl
{
    private TextBox? _textBoxSearch;

    public PdfSearchPanelControl()
    {
#if DEBUG
        if (Design.IsDesignMode)
        {
            DataContext = new PdfDocumentViewModel(null, null, null);
        }
#endif
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _textBoxSearch = e.NameScope.FindFromNameScope<TextBox>("PART_TextBoxSearch");
        _textBoxSearch.KeyDown += TextBoxSearch_OnKeyDown;
        _textBoxSearch.Loaded += TextBox_Loaded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_textBoxSearch is not null)
        {
            _textBoxSearch.Loaded += TextBox_Loaded;
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (_textBoxSearch is not null)
        {
            _textBoxSearch.KeyDown -= TextBoxSearch_OnKeyDown;
            _textBoxSearch.Loaded -= TextBox_Loaded;
        }
    }

    private static void TextBox_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        textBox.Loaded -= TextBox_Loaded;

        if (!textBox.Focus())
        {
            System.Diagnostics.Debug.WriteLine("Something wrong happened while setting focus on search box.");
        }
    }

    private static void TextBoxSearch_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && e.Key == Key.Escape)
        {
            textBox.Clear();
        }
    }
}