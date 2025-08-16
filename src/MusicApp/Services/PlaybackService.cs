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
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Helpers;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

internal class PlaybackService : IPlaybackService, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly IMetadataService metadataService;

    private readonly PlaybackPosition position;
    private readonly BehaviorSubject<int> durationSubject, volumeSubject;
    private readonly BehaviorSubject<PlaybackState> playbackStateSubject;
    private readonly BehaviorSubject<bool> canGoPreviousSubject, canGoNextSubject, shuffleModeSubject, repeatModeSubject;

    private readonly BehaviorSubject<IImmutableList<MediaItem>> shuffledItemsSubject;

    private readonly BehaviorSubject<MediaItem> mediaItemSubject;
    private readonly BehaviorSubject<ImageData> mediaItemCoverSubject;

    private readonly MediaPlayer mediaPlayer;
    private readonly MediaPlaybackList playbackList;

    public PlaybackService(IMetadataService metadataService)
    {
        ArgumentNullException.ThrowIfNull(metadataService);
        this.metadataService = metadataService;

        mediaPlayer = new MediaPlayer();
        playbackList = new MediaPlaybackList
        {
            MaxPlayedItemsToKeepOpen = 0
        };

        position = new PlaybackPosition(this).DisposeWith(disposable);

        durationSubject = new BehaviorSubject<int>(0);
        volumeSubject = new BehaviorSubject<int>(Convert.ToInt32(mediaPlayer.Volume * 100));
        playbackStateSubject = new BehaviorSubject<PlaybackState>(GetPlaybackState());
        canGoPreviousSubject = new BehaviorSubject<bool>(false);
        canGoNextSubject = new BehaviorSubject<bool>(false);
        shuffleModeSubject = new BehaviorSubject<bool>(playbackList.ShuffleEnabled);
        repeatModeSubject = new BehaviorSubject<bool>(playbackList.AutoRepeatEnabled);

        shuffledItemsSubject = new BehaviorSubject<IImmutableList<MediaItem>>(GetShuffledItems());

        mediaItemSubject = new BehaviorSubject<MediaItem>(Core.Models.MediaItem.Empty);
        mediaItemCoverSubject = new BehaviorSubject<ImageData>(Core.Models.ImageData.Empty);

        Items = new MediaItemCollection(this);
        Items.CollectionChanged += Items_CollectionChanged;

        ShuffledItems = shuffledItemsSubject.AsObservable();

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

    public ItemCollectionBase<MediaItem> Items { get; }

    public IObservable<IImmutableList<MediaItem>> ShuffledItems { get; }

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
        SetMediaItem(mediaItem);

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

    public void SetMediaItem(MediaItem? mediaItem)
    {
        var itemIndex = playbackList.Items.IndexOf(FindPlaybackItem(mediaItem));

        if (itemIndex > -1)
        {
            playbackList.MoveTo((uint)itemIndex);
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

    public void SetShuffleMode(IEnumerable<MediaItem> shuffledItems)
    {
        if (shuffledItems?.Any() != true)
        {
            SetShuffleMode(false);
        }
        else
        {
            var set = new HashSet<MediaItem>(shuffledItems);
            if (set.Count != Items.Count || set.Any(x => Items.Contains(x) is false))
            {
                throw new ArgumentException("The shuffled elements do not match the collection of elements");
            }

            var items = shuffledItems.Select(x => FindPlaybackItem(x));
            playbackList.SetShuffledItems(items);

            shuffleModeSubject.OnNext(true);
            shuffledItemsSubject.OnNext(GetShuffledItems());
        }
    }

    public void SetShuffleMode(bool isShuffleMode)
    {
        if (playbackList.ShuffleEnabled != isShuffleMode)
        {
            playbackList.ShuffleEnabled = isShuffleMode;
            shuffleModeSubject.OnNext(isShuffleMode);

            UpdateNavigationState();
            shuffledItemsSubject.OnNext(GetShuffledItems());
        }
    }

    public void SetRepeatMode(bool isRepeatMode)
    {
        if (playbackList.AutoRepeatEnabled != isRepeatMode)
        {
            playbackList.AutoRepeatEnabled = isRepeatMode;
            repeatModeSubject.OnNext(isRepeatMode);
        }
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
        Observable
            .FromEventPattern<object>(mediaPlayer, nameof(MediaPlayer.VolumeChanged))
            .Select(x => Convert.ToInt32(mediaPlayer.Volume * 100))
            .Where(x => x >= 0)
            .Subscribe(x => volumeSubject.OnNext(x))
            .DisposeWith(disposable);

        Observable
            .FromEventPattern<object>(mediaPlayer, nameof(MediaPlayer.CurrentStateChanged))
            .Subscribe(_ => playbackStateSubject.OnNext(GetPlaybackState()))
            .DisposeWith(disposable);

        Observable
            .FromEventPattern<CurrentMediaPlaybackItemChangedEventArgs>(playbackList, nameof(MediaPlaybackList.CurrentItemChanged))
            .Select(x => x.EventArgs.NewItem)
            .Where(x => x != null)
            .Subscribe(async x => await SetCurrentPlaybackItem(x))
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

    private async void Items_CollectionChanged(object? sender, EventArgs e)
    {
        shuffledItemsSubject.OnNext(GetShuffledItems());

        if (mediaPlayer.Source == null && playbackList.Items.Count > 0)
        {
            mediaPlayer.Source = playbackList;
        }

        if (mediaPlayer.Source != null && playbackList.Items.Count == 0)
        {
            mediaPlayer.Source = null;
            await SetCurrentPlaybackItem(null);
        }
    }

    private ImmutableArray<MediaItem> GetShuffledItems()
    {
        return playbackList.ShuffleEnabled
            ? playbackList.ShuffledItems.Select(x => x.Source.GetProperty<MediaItem>()!).ToImmutableArray()
            : [];
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

    private async Task SetCurrentPlaybackItem(MediaPlaybackItem? playbackItem)
    {       
        var mediaItem = playbackItem?.Source.GetProperty<MediaItem>() ?? Core.Models.MediaItem.Empty;
        mediaItemSubject.OnNext(mediaItem);

        UpdateNavigationState();

        if (mediaItemCoverSubject.Value is IDisposable currentCover)
        {
            currentCover.Dispose();
        }

        var cover = await metadataService.LoadMediaCoverAsync(mediaItem);
        mediaItemCoverSubject.OnNext(cover);

        //await UpdateSmtc(cover);

        var duration = playbackItem?.Source?.IsOpen == true
            ? Convert.ToInt32(playbackItem.Source.Duration?.TotalSeconds ?? 0)
            : 0;
        durationSubject.OnNext(duration);
    }

    private void UpdateNavigationState()
    {
        var items = shuffleModeSubject.Value
            ? playbackList.ShuffledItems.ToArray()
            : playbackList.Items.ToArray();

        var index = Array.IndexOf(items, playbackList.CurrentItem);

        canGoPreviousSubject.OnNext(index > 0);
        canGoNextSubject.OnNext(index > -1 && index < items.Length - 1);
    }

    private sealed class MediaItemCollection : ItemCollectionBase<MediaItem>
    {
        private readonly PlaybackService owner;
        private IImmutableList<MediaItem>? mediaItems;

        public MediaItemCollection(PlaybackService owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            this.owner = owner;
        }

        public override IImmutableList<MediaItem> List
        {
            get
            {
                if (mediaItems == null)
                {
                    mediaItems = owner.playbackList
                        .Items
                        .Select(x => x.Source.GetProperty<MediaItem>())
                        .Where(x => x is not null)
                        .ToImmutableArray();
                }

                return mediaItems;
            }
        }

        public override int Count => owner.playbackList.Items.Count;

        public override bool Contains(MediaItem item)
        {
            return owner.FindPlaybackItem(item) is not null;
        }

        protected override void Clear()
        {
            owner.playbackList.Items
                .Select(x => x.Source as IDisposable)
                .Where(x => x is not null)
                .ForEach(x => x.Dispose());

            owner.playbackList.Items.Clear();
        }

        protected override int IndexOf(MediaItem item)
        {
            var items = owner.playbackList.Items;

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Source.GetProperty<MediaItem>()?.Equals(item) == true)
                {
                    return i;
                }
            }

            return -1;
        }

        protected override async Task InsertAsync(MediaItem item, int? index = null)
        {
            owner.playbackList.Items.Add(await LoadPlaybackItem(item));
        }

        protected override void RemoveAt(int index)
        {
            owner.playbackList.Items.RemoveAt(index);
        }

        protected override void OnCollectionChanged()
        {
            base.OnCollectionChanged();

            mediaItems = null;
        }
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
