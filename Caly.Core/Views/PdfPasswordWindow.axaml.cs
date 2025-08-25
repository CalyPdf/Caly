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
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Caly.Core.Views
{
    public sealed partial class PdfPasswordWindow : Window
    {
        private TextBox? _textBoxPassword;

        public PdfPasswordWindow()
        {
            InitializeComponent();
        }

        [MemberNotNull(nameof(_textBoxPassword))]
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _textBoxPassword = this.Find<TextBox>("PART_TextBoxPassword")!;
            ArgumentNullException.ThrowIfNull(_textBoxPassword, nameof(_textBoxPassword));
            _textBoxPassword.Loaded += TextBox_Loaded;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void OkButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxPassword?.Text))
            {
                Close(_textBoxPassword.Text);
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
                System.Diagnostics.Debug.WriteLine("Something wrong happened while setting focus on password box.");
            }
        }
    }
}
