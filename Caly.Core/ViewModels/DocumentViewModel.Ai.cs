// Copyright (c) BobLd
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
using System.ComponentModel;
using System.Threading.Tasks;
using Caly.Core.Models.Ai;
using Caly.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.ViewModels;

public sealed partial class DocumentViewModel
{
    /// <summary>
    /// The AI tab, or <c>null</c> when this build has no <see cref="IDocumentAiService"/>:
    /// the tab is then hidden.
    /// </summary>
    public AiPanelViewModel? Ai { get; }

    private AiPanelViewModel? CreateAiPanel(IDocumentAiService? service)
    {
        if (service is null)
        {
            return null;
        }

        // Settings are loaded by the time a document opens. Without an app (tests) the tab is on.
        if (App.Current?.Services?.GetService<ISettingsService>()?.GetSettings().Ai.Enabled == false)
        {
            return null;
        }

        var panel = new AiPanelViewModel(service, _pdfService,
            () => SelectedPageNumber ?? 1,
            () => PageCount,
            text => App.Current?.Services?.GetService<IClipboardService>()?.SetAsync(text) ?? Task.CompletedTask,
            async (fileName, data) =>
            {
                if (App.Current?.Services?.GetService<IFilesService>() is { } files)
                {
                    await files.SaveFileAsync(data, fileName);
                }
            });

        panel.Items.CollectionChanged += (_, _) => ApplyAiBoxes(panel);
        panel.PropertyChanged += (_, e) => OnAiPanelPropertyChanged(panel, e);
        return panel;
    }

    private void OnAiPanelPropertyChanged(AiPanelViewModel panel, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AiPanelViewModel.SelectedItem))
        {
            return;
        }

        ApplyAiBoxes(panel);

        if (panel.SelectedItem is { } item && item.PageNumber >= 1 && item.PageNumber <= PageCount)
        {
            SelectedPageNumber = item.PageNumber;
        }
    }

    /// <summary>
    /// Puts the boxes of the AI results on the pages they belong to. Items without a box are
    /// only listed in the tab.
    /// </summary>
    private void ApplyAiBoxes(AiPanelViewModel panel)
    {
        var byPage = new Dictionary<int, List<AiPageBox>>();
        for (int i = 0; i < panel.Items.Count; i++)
        {
            var item = panel.Items[i];
            if (item.Box is not { } box)
            {
                continue;
            }

            if (!byPage.TryGetValue(item.PageNumber, out var list))
            {
                byPage[item.PageNumber] = list = [];
            }

            list.Add(new AiPageBox(box, i, ReferenceEquals(item, panel.SelectedItem)));
        }

        foreach (var page in Pages)
        {
            page.AiBoxCommand = panel.SelectItemCommand;
            page.AiBoxes = byPage.TryGetValue(page.PageNumber, out var boxes) ? boxes : null;
        }
    }
}
