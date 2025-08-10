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
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Native;

internal class TaskbarMediaButtonsService : ITaskbarMediaButtonsService
{
    private readonly CompositeDisposable disposable = [];

    private readonly ISystemEventsService systemEventsService;
    private readonly IPlaybackService playbackService;

    private readonly Taskbar taskbar;
    private readonly TaskbarButton previousButton, nextButton, togglePlayButton;
    private IconNative? previousIcon, nextIcon, playIcon, pauseIcon;

    public TaskbarMediaButtonsService(
        ISystemEventsService systemEventsService,
        IPlaybackService playbackService,
        IAppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(systemEventsService);
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(appWindow);

        this.systemEventsService = systemEventsService;
        this.playbackService = playbackService;

        taskbar = new Taskbar(appWindow);

        previousButton = taskbar.AddButton(nameof(previousButton));
        previousButton.ToolTip = "Previous Track";
        previousButton.Command = new RelayCommand(_ => playbackService.GoPrevious());

        togglePlayButton = taskbar.AddButton(nameof(togglePlayButton));
        togglePlayButton.Command = new RelayCommand(_ => playbackService.TogglePlayback());

        nextButton = taskbar.AddButton(nameof(nextButton));
        nextButton.ToolTip = "Next Track";
        nextButton.Command = new RelayCommand(_ => playbackService.GoNext());

        InitSubscriptions();
    }

    public void Dispose()
    {
        if (disposable?.IsDisposed is false)
        {
            disposable.Dispose();
        }

        taskbar.Dispose();
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current is null)
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
            .Subscribe(x => LoadIcons(x.IsDarkTheme, x.IconWidth, x.IconHeight))
            .DisposeWith(disposable);

        playbackService
            .State
            .Select(x => x == PlaybackState.Paused)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(isPaused =>
            {
                togglePlayButton.ToolTip = isPaused ? "Play" : "Pause";
                togglePlayButton.Icon = isPaused ? playIcon?[7] : pauseIcon?[7];
            })
            .DisposeWith(disposable);

        playbackService
            .State
            .Select(x => x == PlaybackState.Paused || x == PlaybackState.Playing)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(isActivePlayback => togglePlayButton.IsEnabled = isActivePlayback)
            .DisposeWith(disposable);

        playbackService
            .CanGoPrevious
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(canGoPrevious => previousButton.IsEnabled = canGoPrevious)
            .DisposeWith(disposable);

        playbackService
            .CanGoNext
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(canGoNext => nextButton.IsEnabled = canGoNext)
            .DisposeWith(disposable);
    }

    private async void LoadIcons(bool isDarkTheme, int iconWidth, int iconHeight)
    {
        previousIcon = Load(isDarkTheme ? "Dark.Previous" : "Light.Previous");
        nextIcon = Load(isDarkTheme ? "Dark.Next" : "Light.Next");
        playIcon = Load(isDarkTheme ? "Dark.Play" : "Light.Play");
        pauseIcon = Load(isDarkTheme ? "Dark.Pause" : "Light.Pause");

        var isPaused = await playbackService.State.FirstAsync() == PlaybackState.Paused;

        previousButton.Icon = previousIcon?.ResolveFrame(iconWidth, iconHeight);
        nextButton.Icon = nextIcon?.ResolveFrame(iconWidth, iconHeight);
        togglePlayButton.Icon = (isPaused ? playIcon : pauseIcon)?.ResolveFrame(iconWidth, iconHeight);
    }

    private IconNative Load(string name)
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream($"MusicApp.Assets.{name}.ico");

        if (stream == null)
        {
            throw new InvalidOperationException($"Can't load {name} icon");
        }

        return new IconNative(stream);
    }
}
