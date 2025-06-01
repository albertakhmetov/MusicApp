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
namespace MusicApp.Core.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicApp.Core.Models;
using MusicApp.Core.Services;

public class PlayerViewModel : ViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = [];
    private readonly IPlaybackService playbackService;

    private MediaItem mediaItem;
    private ImageData mediaItemCover;

    private int position, duration, volume;
    private bool canGoPrevious, canGoNext, isPlaying, isPaused;

    public PlayerViewModel(IPlaybackService playbackService)
    {
        ArgumentNullException.ThrowIfNull(playbackService);

        this.playbackService = playbackService;

        mediaItem = MediaItem.Empty;
        mediaItemCover = ImageData.Empty;

        GoPreviousCommand = new RelayCommand(_ => { });
        GoNextCommand = new RelayCommand(_ => { });
        TogglePlaybackCommand = new RelayCommand(_ => playbackService.TogglePlayback());
        PositionCommand = new RelayCommand(SetPosition);
        VolumeCommand = new RelayCommand(SetVolume);

        InitSubscriptions();
    }

    public MediaItem MediaItem
    {
        get => mediaItem;
        private set => Set(ref mediaItem, value);
    }

    public ImageData MediaItemCover
    {
        get => mediaItemCover;
        private set => Set(ref mediaItemCover, value);
    }

    public int Position
    {
        get => position;
        private set => Set(ref position, value);
    }

    public int Duration
    {
        get => duration;
        private set => Set(ref duration, value);
    }

    public int Volume
    {
        get => volume;
        private set => Set(ref volume, value);
    }

    public bool CanGoPrevious
    {
        get => canGoPrevious;
        private set => Set(ref canGoPrevious, value);
    }

    public bool CanGoNext
    {
        get => canGoNext;
        private set => Set(ref canGoNext, value);
    }

    public bool IsPlaying
    {
        get => isPlaying;
        private set => Set(ref isPlaying, value);
    }

    public bool IsPaused
    {
        get => isPaused;
        private set => Set(ref isPaused, value);
    }

    public bool IsActivePlayback => IsPlaying || IsPaused;

    public ICommand GoPreviousCommand { get; }

    public ICommand GoNextCommand { get; }

    public ICommand TogglePlaybackCommand { get; }

    public ICommand PositionCommand { get; }

    public ICommand VolumeCommand { get; }

    public void Dispose()
    {
        if (!disposable.IsDisposed)
        {
            disposable.Dispose();
        }
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        playbackService
            .Position
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => Position = x)
            .DisposeWith(disposable);

        playbackService
            .Duration
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => Duration = x)
            .DisposeWith(disposable);

        playbackService
            .Volume
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => Volume = x)
            .DisposeWith(disposable);

        playbackService
            .MediaItem
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => MediaItem = x)
            .DisposeWith(disposable);

        playbackService
            .MediaItemCover
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => MediaItemCover = x)
            .DisposeWith(disposable);

        playbackService
            .State
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x =>
            {
                IsPlaying = x == PlaybackState.Playing;
                IsPaused = x == PlaybackState.Paused;
                Invalidate(nameof(IsActivePlayback));
            })
            .DisposeWith(disposable);
    }

    private void SetPosition(object? newValue)
    {
        if (newValue is int value)
        {
            playbackService.SetPosition(value);
        }
    }

    private void SetVolume(object? newValue)
    {
        if (newValue is int value)
        {
            playbackService.SetVolume(value);
        }
    }
}
