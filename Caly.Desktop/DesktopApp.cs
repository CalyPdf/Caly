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
using Caly.Core;
using Caly.Core.Services.Interfaces;
using Caly.Printing.Unix;
using Caly.Printing.Windows;
using Microsoft.Extensions.DependencyInjection;
#if CALY_AI
using Caly.Ai.FoundryLocal;
using Caly.Core.Services;
#endif

namespace Caly.Desktop;

/// <summary>
/// Desktop-specific <see cref="App"/> subclass that registers the correct
/// platform print service implementation.
/// </summary>
internal sealed class DesktopApp : App
{
    protected override void OverrideRegisteredServices(IServiceCollection services)
    {
#if CALY_AI
        // Registered whatever the settings say: the tab checks CalySettings.Ai.Enabled once settings
        // are loaded. Nothing is started until the user runs an AI action.
        services.AddSingleton<IDocumentAiService>(sp =>
            new FoundryLocalDocumentAiService(sp.GetRequiredService<ISettingsService>().GetSettings().Ai.ModelId));
#endif

        if (OperatingSystem.IsWindows())
        {
            if (OperatingSystem.IsOSPlatformVersionAtLeast("windows", 5, 0))
            {
                services.AddSingleton<IPrintService, WindowsPrintService>();
            }
            else
            {
                // Not supported
            }
        }
        else
        {
            services.AddSingleton<IPrintService, UnixPrintService>();
        }
    }
}
