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
using Avalonia.Controls.Notifications;
using Caly.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using UglyToad.PdfPig.Logging;

namespace Caly.Core.Utilities;

internal sealed class CalyPdfPigLogger : ILog
{
    private const string AnnotationTitle = "Error in pdf document";

    public static readonly CalyPdfPigLogger Instance = new CalyPdfPigLogger();

    public void Debug(string message)
    {
    }

    public void Debug(string message, Exception ex)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message)
    {
        App.Messenger.Send(new ShowNotificationMessage(NotificationType.Warning, AnnotationTitle, message));
    }

    public void Error(string message, Exception ex)
    {
        // We ignore the ex for the moment
        App.Messenger.Send(new ShowNotificationMessage(NotificationType.Warning, AnnotationTitle, message));
    }
}
