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
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Extensions;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

internal class PlaybackService : IPlaybackService, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly PlaybackPosition position;
    private readonly BehaviorSubject<int> durationSubject, volumeSubject;
    private readonly BehaviorSubject<PlaybackState> playbackStateSubject;
    private readonly BehaviorSubject<bool> canGoPreviousSubject, canGoNextSubject, shuffleModeSubject, repeatModeSubject;

    private readonly BehaviorSubject<MediaItem> mediaItemSubject;
    private readonly BehaviorSubject<ImageData> mediaItemCoverSubject;

    private readonly MediaPlayer mediaPlayer;
    private readonly MediaPlaybackList playbackList;

    public PlaybackService()
    {
        mediaPlayer = new MediaPlayer();
        playbackList = new MediaPlaybackList();

        position = new PlaybackPosition(this).DisposeWith(disposable);

        durationSubject = new BehaviorSubject<int>(0);
        volumeSubject = new BehaviorSubject<int>(Convert.ToInt32(mediaPlayer.Volume * 100));
        playbackStateSubject = new BehaviorSubject<PlaybackState>(GetPlaybackState());
        canGoPreviousSubject = new BehaviorSubject<bool>(false);
        canGoNextSubject = new BehaviorSubject<bool>(false);
        shuffleModeSubject = new BehaviorSubject<bool>(false);
        repeatModeSubject = new BehaviorSubject<bool>(false);

        mediaItemSubject = new BehaviorSubject<MediaItem>(Core.Models.MediaItem.Empty);
        mediaItemCoverSubject = new BehaviorSubject<ImageData>(Core.Models.ImageData.Empty);

        Items = new ItemCollection<MediaItem>();

        MediaItem = mediaItemSubject.AsObservable();
        MediaItemCover = mediaItemCoverSubject.AsObservable();

        Position = position
            .StartWith(mediaPlayer.PlaybackSession.Position)
            .Select(x => Convert.ToInt32(x.TotalSeconds))
            .AsObservable();

        Duration = durationSubject.AsObservable();
        Volume = volumeSubject.AsObservable();
        State = playbackStateSubject.AsObservable();
        CanGoPrevious = canGoPreviousSubject.AsObservable();
        CanGoNext = canGoNextSubject.AsObservable();
        ShuffleMode = shuffleModeSubject.AsObservable();
        RepeatMode = repeatModeSubject.AsObservable();

        InitSubscriptions();
    }

    public IObservable<MediaItem> MediaItem { get; }

    public IObservable<ImageData> MediaItemCover { get; }

    public ItemCollection<MediaItem> Items { get; }

    public IObservable<int> Position { get; }

    public IObservable<int> Duration { get; }

    public IObservable<int> Volume { get; }

    public IObservable<PlaybackState> State { get; }

    public IObservable<bool> CanGoPrevious { get; }

    public IObservable<bool> CanGoNext { get; }

    public IObservable<bool> ShuffleMode { get; }

    public IObservable<bool> RepeatMode { get; }

    public void Play(MediaItem? mediaItem)
    {
        var itemIndex = playbackList.Items.IndexOf(FindPlaybackItem(mediaItem));

        if (itemIndex > -1)
        {
            playbackList.MoveTo((uint)itemIndex);
        }

        Play();
    }

    public void Play()
    {
        mediaPlayer.Play();
    }

    public void Pause()
    {
        mediaPlayer.Pause();
    }

    public void TogglePlayback()
    {
        switch (playbackStateSubject.Value)
        {
            case PlaybackState.Stopped:
            case PlaybackState.Paused:
                Play();
                break;

            case PlaybackState.Playing:
                Pause();
                break;
        }
    }

    public void GoPrevious()
    {
        playbackList.MovePrevious();
    }

    public void GoNext()
    {
        playbackList.MoveNext();
    }

    public void SetPosition(int newPosition)
    {
        position.SetPosition(TimeSpan.FromSeconds(newPosition));
    }

    public void SetVolume(int volume)
    {
        var newVolume = Math.Max(0, Math.Min(100, volume));

        if (volumeSubject.Value != newVolume)
        {
            mediaPlayer.Volume = newVolume / 100d;
        }
    }

    public void SetShuffleMode(bool isShuffleMode)
    {
        throw new NotImplementedException();
    }

    public void SetRepeatMode(bool isRepeatMode)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        if (!disposable.IsDisposed)
        {
            disposable.Dispose();
            mediaItemCoverSubject.Value.Dispose();
            mediaPlayer.Dispose();
        }
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        Items
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(async x => await UpdatePlaylist(x))
            .DisposeWith(disposable);

        Observable
            .FromEventPattern<object>(mediaPlayer, nameof(MediaPlayer.VolumeChanged))
            .Select(x => Convert.ToInt32(mediaPlayer.Volume * 100))
            .Where(x => x >= 0)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => volumeSubject.OnNext(x))
            .DisposeWith(disposable);

        Observable
            .FromEventPattern<object>(mediaPlayer, nameof(MediaPlayer.CurrentStateChanged))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ => playbackStateSubject.OnNext(GetPlaybackState()))
            .DisposeWith(disposable);

        Observable
            .FromEventPattern<CurrentMediaPlaybackItemChangedEventArgs>(playbackList, nameof(MediaPlaybackList.CurrentItemChanged))
            .Select(x => x.EventArgs.NewItem)
            .Where(x => x != null)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(async x => await SetCurrentItem(x))
            .DisposeWith(disposable);
    }

    private PlaybackState GetPlaybackState() => mediaPlayer.CurrentState switch
    {
        MediaPlayerState.Closed => PlaybackState.Closed,
        MediaPlayerState.Opening => PlaybackState.Opening,
        MediaPlayerState.Buffering => PlaybackState.Buffering,
        MediaPlayerState.Playing => PlaybackState.Playing,
        MediaPlayerState.Paused => PlaybackState.Paused,
        MediaPlayerState.Stopped => PlaybackState.Stopped,
        _ => throw new NotSupportedException($"{mediaPlayer.CurrentState} isn't supported"),
    };

    private async Task UpdatePlaylist(ItemCollection<MediaItem>.CollectionAction action)
    {
        switch (action.Type)
        {
            case ItemCollection<MediaItem>.CollectionActionType.Reset:
                playbackList.Items
                    .Select(x => x.Source as IDisposable)
                    .Where(x => x != null)
                    .ForEach(x => x.Dispose());

                playbackList.Items.Clear();

                foreach (var i in action.Items)
                {
                    playbackList.Items.Add(await LoadPlaybackItem(i));
                }

                break;

            case ItemCollection<MediaItem>.CollectionActionType.Add:
                foreach (var i in action.Items)
                {
                    playbackList.Items.Add(await LoadPlaybackItem(i));
                }

                break;

            case ItemCollection<MediaItem>.CollectionActionType.Remove:
                var itemToRemove = FindPlaybackItem(action.Items[0]);

                if (itemToRemove != null)
                {
                    playbackList.Items.Remove(itemToRemove);
                }

                break;
        }

        if (mediaPlayer.Source == null && playbackList.Items.Count > 0)
        {
            mediaPlayer.Source = playbackList;
        }

        if (mediaPlayer.Source != null && playbackList.Items.Count == 0)
        {
            mediaPlayer.Source = null;
            await SetCurrentItem(null);
        }
    }

    private MediaPlaybackItem? FindPlaybackItem(MediaItem? mediaItem)
    {
        return mediaItem == null ? null : playbackList
            .Items
            .FirstOrDefault(x => x.Source.GetProperty<MediaItem>()?.Equals(mediaItem) == true);
    }

    private static async Task<MediaPlaybackItem> LoadPlaybackItem(MediaItem mediaItem)
    {
        var file = await StorageFile.GetFileFromPathAsync(mediaItem.FileName);
        var mediaSource = MediaSource.CreateFromStorageFile(file);
        mediaSource.SetProperty(mediaItem);

        //  await mediaSource.OpenAsync();

        var item = new MediaPlaybackItem(mediaSource);
        var properties = item.GetDisplayProperties();

        properties.Type = MediaPlaybackType.Music;
        properties.MusicProperties.Title = mediaItem?.Title;
        properties.MusicProperties.AlbumTitle = mediaItem?.Album;
        properties.MusicProperties.AlbumArtist = mediaItem?.Artist;

        item.ApplyDisplayProperties(properties);

        return item;
    }

    private async Task SetCurrentItem(MediaPlaybackItem? playbackItem)
    {
        var mediaItem = playbackItem?.Source.GetProperty<MediaItem>() ?? Core.Models.MediaItem.Empty;
        mediaItemSubject.OnNext(mediaItem);

        UpdateNavigationState();

        if (mediaItemCoverSubject.Value is IDisposable currentCover)
        {
            currentCover.Dispose();
        }

        var cover = await mediaItem.LoadCover();
        mediaItemCoverSubject.OnNext(cover);

        //await UpdateSmtc(cover);

        var duration = playbackItem?.Source?.IsOpen == true
            ? Convert.ToInt32(playbackItem.Source.Duration?.TotalSeconds ?? 0)
            : 0;
        durationSubject.OnNext(duration);
    }

    private void UpdateNavigationState()
    {
        var index = playbackList.Items.IndexOf(playbackList.CurrentItem);

        canGoPreviousSubject.OnNext(index > 0);
        canGoNextSubject.OnNext(index > -1 && index < playbackList.Items.Count - 1);
    }

    private sealed class PlaybackPosition : IObservable<TimeSpan>, IDisposable
    {
        private readonly PlaybackService owner;
        private readonly CompositeDisposable disposable = [];
        private readonly IObservable<TimeSpan> positionChanged;

        private volatile bool _isSeeking;

        public PlaybackPosition(PlaybackService owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            this.owner = owner;

            positionChanged = Observable
                .FromEventPattern<object>(owner.mediaPlayer.PlaybackSession, nameof(MediaPlaybackSession.PositionChanged))
                .Where(_ => !_isSeeking)
                .Select(x => owner.mediaPlayer.PlaybackSession.Position)
                .Publish()
                .RefCount();

            Observable
                .FromEventPattern<object>(owner.mediaPlayer.PlaybackSession, nameof(MediaPlaybackSession.SeekCompleted))
                .Subscribe(_ => _isSeeking = false)
                .DisposeWith(disposable);
        }

        public void Dispose()
        {
            if (!disposable.IsDisposed)
            {
                disposable.Dispose();
            }
        }

        public IDisposable Subscribe(IObserver<TimeSpan> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);

            return positionChanged.Subscribe(observer);
        }

        public void SetPosition(TimeSpan position)
        {
            _isSeeking = true;
            owner.mediaPlayer.PlaybackSession.Position = position;
        }
    }
}
