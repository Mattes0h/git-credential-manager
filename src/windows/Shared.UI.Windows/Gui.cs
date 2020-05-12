﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Microsoft.Git.CredentialManager.UI
{
    public interface IGui
    {
        /// <summary>
        /// Presents the user with `<paramref name="windowCreator"/>` with the `<paramref name="viewModel"/>`.
        /// <para/>
        /// Returns `<see langword="true"/>` if the user completed the dialog; otherwise `<see langword="false"/>` if the user canceled or abandoned the dialog.
        /// </summary>
        /// <param name="viewModel">The view model passed to the presented window.</param>
        /// <param name="windowCreator">Creates the window `<paramref name="viewModel"/>` is passed to.</param>
        bool ShowViewModel(ViewModel viewModel, Func<Window> windowCreator);

        /// <summary>
        /// Present the user with `<paramref name="windowCreator"/>`.
        /// <para/>
        /// Returns `<see langword="true"/>` if the user completed the dialog; otherwise `<see langword="false"/>` if the user canceled or abandoned the dialog.
        /// </summary>
        /// <param name="windowCreator">Creates the window.</param>
        bool ShowDialogWindow(Func<Window> windowCreator);
    }

    public class Gui : IGui
    {
        private readonly IntPtr _parentHwnd = IntPtr.Zero;

        public Gui()
        {
            string envar = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.GcmParentWindow);

            if (long.TryParse(envar, out long ptrInt))
            {
                _parentHwnd = new IntPtr(ptrInt);
            }
        }

        public bool ShowDialogWindow(Func<Window> windowCreator)
        {
            bool windowResult = false;

            StartSTATask(() =>
            {
                var window = windowCreator();

                windowResult = ShowDialog(window, _parentHwnd) ?? false;
            })
            .Wait();

            return windowResult;
        }

        public bool ShowViewModel(ViewModel viewModel, Func<Window> windowCreator)
        {
            bool windowResult = false;

            StartSTATask(() =>
            {
                var window = windowCreator();

                window.DataContext = viewModel;

                windowResult = ShowDialog(window, _parentHwnd) ?? false;
            })
            .Wait();

            return windowResult && viewModel.IsValid;
        }

        private static Task StartSTATask(Action action)
        {
            var completionSource = new TaskCompletionSource<object>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    completionSource.SetResult(null);
                }
                catch (Exception e)
                {
                    completionSource.SetException(e);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return completionSource.Task;
        }

        public static bool? ShowDialog(Window window, IntPtr parentHwnd)
        {
            // Zero is not a valid window handles
            if (parentHwnd == IntPtr.Zero)
            {
                return window.ShowDialog();
            }

            // Set the parent window handle and ensure the dialog starts in the correct location
            new System.Windows.Interop.WindowInteropHelper(window).Owner = parentHwnd;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            const int ERROR_INVALID_WINDOW_HANDLE = 1400;

            try
            {
                return window.ShowDialog();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_INVALID_WINDOW_HANDLE)
            {
                // The window handle given was invalid - clear the owner and show the dialog centered on the screen
                window.Owner = null;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return window.ShowDialog();
            }
        }
    }
}
