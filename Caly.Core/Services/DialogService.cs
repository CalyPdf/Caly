﻿// Copyright (C) 2024 BobLd
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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Core.Views;

namespace Caly.Core.Services
{
    internal sealed class DialogService : IDialogService
    {
        private readonly TimeSpan _annotationExpiration = TimeSpan.FromSeconds(20);
        private readonly Visual _target;
        private readonly TimeSpan _minDelay = TimeSpan.FromSeconds(3);

        private string? _previousNotificationMessage;
        private DateTime _previousNotificationTime = DateTime.MinValue;
        private string? _previousExceptionWindowMessage;
        private DateTime _previousExceptionWindowTime = DateTime.MinValue;

        private WindowNotificationManager? _windowNotificationManager;

        public DialogService(Visual target)
        {
            _target = target;
            if (_target is Window w)
            {
                w.Loaded += _window_Loaded;
            }
        }

        private void _window_Loaded(object? sender, RoutedEventArgs e)
        {
            if (_target is Window w)
            {
                w.Loaded -= _window_Loaded;
            }

            if (sender is MainWindow mw)
            {
                _windowNotificationManager = mw.NotificationManager;
                System.Diagnostics.Debug.Assert(_windowNotificationManager is not null);
            }
            else
            {
                throw new InvalidOperationException($"Expecting '{typeof(MainWindow)}' but got '{sender?.GetType()}'.");
            }
        }

        public async Task<string?> ShowPdfPasswordDialogAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_target is Window w)
                {
                    return new PdfPasswordWindow().ShowDialog<string?>(w);
                }
                return Task.FromResult<string?>(string.Empty);
            });
        }
        
        public void ShowNotification(string? title, string? message, NotificationType type)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Debug.ThrowNotOnUiThread();
                System.Diagnostics.Debug.WriteLine($"Annotation ({type}): {title}\n{message}");
                if (_windowNotificationManager is not null)
                {
                    DateTime now = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(message) ||
                        (now - _previousNotificationTime <= _minDelay &&
                        message.Equals(_previousNotificationMessage)))
                    {
                        return;
                    }

                    _previousNotificationTime = now;
                    _previousNotificationMessage = message;
                    _windowNotificationManager.Show(new Notification(title, message, type, _annotationExpiration));
                }
                else
                {
                    // TODO - we need a queue system to display the annotations when the manager is loaded
                    System.Diagnostics.Debug.WriteLine($"Annotation (ERROR NOT LOADED) ({type}): {title}\n{message}");
                }
            }, DispatcherPriority.Loaded);
        }
        
        public Task ShowExceptionWindowAsync(Exception exception)
        {
            return ShowExceptionWindowAsync(new ExceptionViewModel(exception));
        }

        public async Task ShowExceptionWindowAsync(ExceptionViewModel exception)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Debug.ThrowNotOnUiThread();
                System.Diagnostics.Debug.WriteLine(exception.ToString());
                if (_target is not Window w)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                if (string.IsNullOrEmpty(exception.Message) ||
                    (now - _previousExceptionWindowTime <= _minDelay &&
                     exception.Message.Equals(_previousExceptionWindowMessage)))
                {
                    return;
                }

                // TODO - Improve to count same messages
                _previousExceptionWindowTime = now;
                _previousExceptionWindowMessage = exception.Message;
                var window = new MessageWindow { DataContext = exception };
                await window.ShowDialog(w);

            }, DispatcherPriority.Loaded);
        }

        public void ShowExceptionWindow(Exception exception)
        {
            ShowExceptionWindow(new ExceptionViewModel(exception));
        }

        public void ShowExceptionWindow(ExceptionViewModel exception)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Debug.ThrowNotOnUiThread();
                System.Diagnostics.Debug.WriteLine(exception.ToString());

                if (exception.Message != _previousExceptionWindowMessage) // TODO - Improve to count same messages
                {
                    var window = new MessageWindow { DataContext = exception };
                    window.Show();
                    _previousExceptionWindowMessage = exception.Message;
                }
            }, DispatcherPriority.Loaded);
        }
    }
}
