// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using Avalonia.Controls.Notifications;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Logging;

namespace Caly.Core.Loggers
{
    internal sealed class CalyPdfPigLogger : ILog
    {
        private const string _annotationTitle = "Error in pdf document";

        private readonly ILogger<PdfPigPdfService> _logger;
        private readonly IDialogService _dialogService;

        public CalyPdfPigLogger(IDialogService dialogService, ILogger<PdfPigPdfService> logger)
        {
            ArgumentNullException.ThrowIfNull(dialogService, nameof(dialogService));
            _logger = logger;
            _dialogService = dialogService;
        }

        public void Debug(string message)
        {
            _logger.LogDebug("PdfPig: {message}", message);
        }

        public void Debug(string message, Exception ex)
        {
            _logger.LogDebug(ex, "PdfPig: {message}", message);
        }

        public void Warn(string message)
        {
            _logger.LogWarning("PdfPig: {message}", message);
        }

        public void Error(string message)
        {
            _logger.LogError("PdfPig: {message}", message);
            _dialogService.ShowNotification(_annotationTitle, message, NotificationType.Warning);
        }

        public void Error(string message, Exception ex)
        {
            _logger.LogError(ex, "PdfPig: {message}", message);
            _dialogService.ShowNotification(_annotationTitle, message, NotificationType.Warning);
        }
    }
}