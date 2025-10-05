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
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Caly.Core.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace Caly.Core.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            AddHandler(DragDrop.DropEvent, Drop);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Pass KeyBindings to top level
            if (TopLevel.GetTopLevel(this) is Window w)
            {
                w.KeyBindings.AddRange(KeyBindings);
            }
        }

        private static async void Drop(object? sender, DragEventArgs e)
        {
            try
            {
                if (!e.DataTransfer.Contains(DataFormat.File))
                {
                    return;
                }

                var files = e.DataTransfer.TryGetFiles();

                if (files is null)
                {
                    return;
                }

                _ = await App.Messenger.Send(new OpenLoadDocumentsRequestMessage(files, CancellationToken.None));
            }
            catch (Exception ex)
            {
                // TODO - Show dialog
                Debug.WriteExceptionToFile(ex);
            }
        }
    }
}
