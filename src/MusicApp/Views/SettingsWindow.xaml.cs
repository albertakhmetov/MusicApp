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

public sealed partial class SettingsWindow : Window
{
    private readonly CompositeDisposable disposable = [];
    private readonly ISettingsService settingsService;
    private readonly ISystemEventsService systemEventsService;

    public SettingsWindow(ISettingsService settingsService, ISystemEventsService systemEventsService, SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(systemEventsService);
        ArgumentNullException.ThrowIfNull(viewModel);

        this.settingsService = settingsService;
        this.systemEventsService = systemEventsService;

        ViewModel = viewModel;

        InitializeComponent();

        var presenter = OverlappedPresenter.Create();
        presenter.PreferredMinimumWidth = 600;
        presenter.PreferredMaximumWidth = 800;
        presenter.PreferredMinimumHeight = 600;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(true, true);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        ExtendsContentIntoTitleBar = true;

        SetTitleBar(AppTitleBar);

        Closed += OnClosed;
        InitSubscriptions();
    }

    public SettingsViewModel ViewModel { get; }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        Observable
            .CombineLatest(
                settingsService.WindowTheme,
                systemEventsService.AppDarkTheme,
                (theme, isSystemDark) => theme == WindowTheme.Dark || theme == WindowTheme.System && isSystemDark)
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(isDarkTheme => this.UpdateTheme(isDarkTheme))
            .DisposeWith(disposable);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (!disposable.IsDisposed)
        {
            disposable.Dispose();
        }
    }
}
