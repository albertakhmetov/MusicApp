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
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicApp.Core.Commands;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;

public class PlaylistViewModel : ViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly IShellService shellService;
    private readonly IFileService fileService;
    private readonly IPlaybackService playbackService;
    private readonly IAppService appService;
    private readonly IAppCommandManager appCommandManager;

    private readonly ItemObservableCollection<PlaylistItemViewModel> items;
    private MediaItem? currentItem;
    private bool isShuffleMode, isRepeatMode;

    public PlaylistViewModel(
        IShellService shellService,
        IFileService fileService,
        IPlaybackService playbackService,
        IAppService appService,
        IAppCommandManager appCommandManager)
    {
        ArgumentNullException.ThrowIfNull(shellService);
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(appService);
        ArgumentNullException.ThrowIfNull(appCommandManager);

        this.shellService = shellService;
        this.fileService = fileService;
        this.playbackService = playbackService;
        this.appService = appService;
        this.appCommandManager = appCommandManager;

        items = [];
        Items = new ReadOnlyObservableCollection<PlaylistItemViewModel>(items);

        AddCommand = new RelayCommand(_ => SelectAndAddItemsAsync());
        RemoveCommand = new RelayCommand(x => RemoveMediaItem(x as MediaItem));
        RemoveAllCommand = new RelayCommand(_ => RemoveAllItems());

        PlayCommand = new RelayCommand(x => playbackService.Play(x as MediaItem));

        AddItemsCommand = new RelayCommand(x => AddMediaItems(x as IList<string>, overwrite: false));
        ReplaceItemsCommand = new RelayCommand(x => AddMediaItems(x as IList<string>, overwrite: true));

        InitSubscriptions();
    }

    public ReadOnlyObservableCollection<PlaylistItemViewModel> Items { get; }

    public bool IsEmpty => Items.Count == 0;

    public MediaItem? CurrentItem
    {
        get => currentItem;
        private set
        {
            if (Set(ref currentItem, value))
            {
                Items.ForEach(x => x.Invalidate(nameof(PlaylistItemViewModel.IsCurrent)));
            }
        }
    }

    public bool IsShuffleMode
    {
        get => isShuffleMode;
        set => playbackService.SetShuffleMode(value);
    }

    public bool IsRepeatMode
    {
        get => isRepeatMode;
        set => playbackService.SetRepeatMode(value);
    }

    public ICommand AddCommand { get; }

    public ICommand AddItemsCommand { get; }

    public ICommand ReplaceItemsCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand PlayCommand { get; }

    public ICommand RemoveCommand { get; }

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
            .Items
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(UpdatePlaylist)
            .DisposeWith(disposable);

        playbackService
            .MediaItem
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => CurrentItem = x)
            .DisposeWith(disposable);

        playbackService
            .ShuffleMode
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x =>
            {
                isShuffleMode = x;
                Invalidate(nameof(IsShuffleMode));
            })
            .DisposeWith(disposable);

        playbackService
            .RepeatMode
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x =>
            {
                isRepeatMode = x;
                Invalidate(nameof(IsRepeatMode));
            }).DisposeWith(disposable);
    }

    private void UpdatePlaylist(ItemCollectionAction<MediaItem> action)
    {
        switch (action.Type)
        {
            case ItemCollectionActionType.Reset:
                items.Set(action.Items.Select(i => new PlaylistItemViewModel(this, i)));
                break;

            case ItemCollectionActionType.Add:
                items.Insert(action.Items.Select(i => new PlaylistItemViewModel(this, i)), null);
                break;

            case ItemCollectionActionType.Remove:
                var itemToRemove = items.FirstOrDefault(x => x.MediaItem.Equals(action.Items[0]));

                if (itemToRemove != null)
                {
                    items.Remove(itemToRemove);
                }

                break;
        }

        Invalidate(nameof(IsEmpty));
    }

    private async void SelectAndAddItemsAsync()
    {
        var selectedFiles = await appService.PickFilesForOpenAsync(shellService.SupportedFileTypes);

        if (selectedFiles?.Any() != true)
        {
            return;
        }

        var items = await fileService.LoadMediaItems(selectedFiles);

        await appCommandManager.ExecuteAsync(new MediaItemAddCommand.Parameters
        {
            Items = items.ToImmutableArray()
        });
    }

    private async void RemoveAllItems()
    {
        await appCommandManager.ExecuteAsync(new MediaItemRemoveCommand.Parameters
        {
            RemoveAll = true
        });
    }

    private async void AddMediaItems(IList<string>? fileNames, bool overwrite)
    {
        if (fileNames?.Any() != true)
        {
            return;
        }

        var items = await fileService.LoadMediaItems(fileNames);

        await appCommandManager.ExecuteAsync(new MediaItemAddCommand.Parameters
        {
            Items = items.ToImmutableArray(),
            Overwrite = overwrite
        });
    }

    private async void RemoveMediaItem(MediaItem? mediaItem)
    {
        if (mediaItem is null)
        {
            return;
        }

        await appCommandManager.ExecuteAsync(new MediaItemRemoveCommand.Parameters
        {
            Item = mediaItem
        });
    }
}
