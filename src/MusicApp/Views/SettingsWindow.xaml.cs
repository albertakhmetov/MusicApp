/*  Copyright © 2025, Albert Akhmetov <akhmetov@live.com>   
 *
 *  This file is part of MusicApp.
 *
 *  MusicApp is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  MusicApp is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with MusicApp. If not, see <https://www.gnu.org/licenses/>.   
 *
 */
namespace MusicApp.Views;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using System.ComponentModel;
using MusicApp.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Composition;

public sealed partial class SettingsWindow : Window, IAppWindow
{
    private readonly IHostApplicationLifetime lifetime;

    public SettingsWindow(IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);

        this.lifetime = lifetime;

        InitializeComponent();

        var presenter = OverlappedPresenter.Create();
        presenter.PreferredMinimumWidth = 600;
        presenter.PreferredMaximumWidth = 800;
        presenter.PreferredMinimumHeight = 600;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        //   presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(true, true);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        ExtendsContentIntoTitleBar = true;

        if (Content is Grid grid)
        {
            var titleBar = grid.FindName("TitleBar") as TitleBar;
            SetTitleBar(titleBar);
        }

        AppWindow.Resize(AppWindow.Size);
        AppWindow.Closing += OnWindowClosing;

        base.Closed += OnWindowClosed;
    }

    public nint Handle => WindowNative.GetWindowHandle(this);

    public new event EventHandler? Closed;

    public void Show()
    {
        AppWindow.Show();
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var isAppStopped = lifetime.ApplicationStopping.IsCancellationRequested || lifetime.ApplicationStopped.IsCancellationRequested;

        if (isAppStopped is false)
        {
            args.Cancel = true;
            AppWindow.Hide();
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
