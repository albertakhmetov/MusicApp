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
namespace MusicApp.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Native;

internal class TaskbarMediaButtonsService : IAppWindowService, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly ISystemEventsService systemEventsService;
    private readonly IPlaybackService playbackService;

    private IAppWindow? appWindow;
    private Taskbar? taskbar;
    private TaskbarButton? previousButton, nextButton, playButton, pauseButton;

    public TaskbarMediaButtonsService(
        ISystemEventsService systemEventsService,
        IPlaybackService playbackService)
    {
        ArgumentNullException.ThrowIfNull(systemEventsService);
        ArgumentNullException.ThrowIfNull(playbackService);

        this.systemEventsService = systemEventsService;
        this.playbackService = playbackService;
    }

    public void Dispose()
    {
        if (disposable?.IsDisposed is false)
        {
            disposable.Dispose();
        }

        taskbar?.Dispose();
    }

    public void Init(IAppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        appWindow = window;

        InitSubscriptions();
    }

    private void InitTaskbar(bool isDarkTheme, int iconWidth, int iconHeight)
    {
        if (appWindow is null)
        {
            throw new InvalidOperationException("Window isn't initialized.");
        }

        var theme = isDarkTheme ? "Dark" : "Light";

        previousButton = new TaskbarButton(LoadIcon($"{theme}.Previous", iconWidth, iconHeight))
        {
            ToolTip = "Previous Track",
            Command = new RelayCommand(_ => playbackService.GoPrevious())
        };

        playButton = new TaskbarButton(LoadIcon($"{theme}.Play", iconWidth, iconHeight))
        {
            ToolTip = "Play",
            Command = new RelayCommand(_ => playbackService.Play())
        };

        pauseButton = new TaskbarButton(LoadIcon($"{theme}.Pause", iconWidth, iconHeight))
        {
            ToolTip = "Pause",
            Command = new RelayCommand(_ => playbackService.Pause())
        };

        nextButton = new TaskbarButton(LoadIcon($"{theme}.Next", iconWidth, iconHeight))
        {
            ToolTip = "Next Track",
            Command = new RelayCommand(_ => playbackService.GoNext())
        };

        taskbar?.Dispose();

        taskbar = new Taskbar(
            appWindow,
            previousButton,
            playButton,
            pauseButton,
            nextButton);
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        Observable
            .CombineLatest(
                systemEventsService.SystemDarkTheme,
                systemEventsService.IconWidth,
                systemEventsService.IconHeight,
                (IsDarkTheme, IconWidth, IconHeight) => new { IsDarkTheme, IconWidth, IconHeight })
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => InitTaskbar(x.IsDarkTheme, x.IconWidth, x.IconHeight))
            .DisposeWith(disposable);

        Observable
            .CombineLatest(
                playbackService.CanGoPrevious.DistinctUntilChanged(),
                playbackService.CanGoNext.DistinctUntilChanged(),
                (CanGoPrevious, CanGoNext) => new { CanGoPrevious, CanGoNext })
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => UpdateNavigation(x.CanGoPrevious, x.CanGoNext))
            .DisposeWith(disposable);

        playbackService
            .State
            .Select(x => x == PlaybackState.Playing)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(UpdatePlaybackState)
            .DisposeWith(disposable);
    }

    private void UpdateNavigation(bool canGoPrevious, bool canGoNext)
    {
        if (previousButton is not null)
        {
            previousButton.IsEnabled = canGoPrevious;
        }

        if (nextButton is not null)
        {
            nextButton.IsEnabled = canGoNext;
        }
    }

    private void UpdatePlaybackState(bool isPlaying)
    {
        if (playButton is not null)
        {
            playButton.IsVisible = !isPlaying;
        }

        if (pauseButton is not null)
        {
            pauseButton.IsVisible = isPlaying;
        }
    }

    private static SafeHandle LoadIcon(string name, int iconWidth, int iconHeight)
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream($"MusicApp.Assets.{name}.ico");

        var nativeIcon = stream is not null
            ? new IconNative(stream)
            : throw new InvalidOperationException($"Can't load {name} icon");

        return nativeIcon.ResolveFrame(iconWidth, iconHeight);
    }
}
