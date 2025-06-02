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
using MusicApp.Core.Models;
using MusicApp.Core.Services;

public class PlaylistViewModel : ViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = [];
    private readonly IPlaybackService playbackService;
    private readonly IFileService fileService;
    private readonly IAppCommandManager commandManager;

    private readonly ItemObservableCollection<PlaylistItemViewModel> items;
    private MediaItem? currentItem;
    private bool isShuffleMode, isRepeatMode;

    public PlaylistViewModel(
        IPlaybackService playbackService,
        IFileService fileService,
        IAppCommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(commandManager);

        this.playbackService = playbackService;
        this.fileService = fileService;
        this.commandManager = commandManager;

        items = [];
        Items = new ReadOnlyObservableCollection<PlaylistItemViewModel>(items);

        AddCommand = new RelayCommand(async _ => await SelectAndAddItems());
        RemoveAllCommand = new RelayCommand(_ => RemoveAllItems());

        PlayCommand = new RelayCommand(x => playbackService.Play(x as MediaItem));
        RemoveCommand = new RelayCommand(x => playbackService.Items.Remove(x as MediaItem));

        InitSubscriptions();
    }

    public ReadOnlyObservableCollection<PlaylistItemViewModel> Items { get; }

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
            .Subscribe(x => IsShuffleMode = x)
            .DisposeWith(disposable);

        playbackService
            .RepeatMode
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x => IsRepeatMode = x)
            .DisposeWith(disposable);
    }

    private void UpdatePlaylist(ItemCollection<MediaItem>.CollectionAction action)
    {
        switch (action.Type)
        {
            case ItemCollection<MediaItem>.CollectionActionType.Reset:
                items.Set(action.Items.Select(i => new PlaylistItemViewModel(this, i)));
                break;

            case ItemCollection<MediaItem>.CollectionActionType.Add:
                items.Insert(action.Items.Select(i => new PlaylistItemViewModel(this, i)), null);
                break;

            case ItemCollection<MediaItem>.CollectionActionType.Remove:
                var itemToRemove = items.FirstOrDefault(x => x.MediaItem.Equals(action.Items[0]));

                if (itemToRemove != null)
                {
                    items.Remove(itemToRemove);
                }

                break;
        }
    }

    private async Task SelectAndAddItems()
    {
        var selectedFiles = await fileService.PickMultipleFilesAsync();

        if (selectedFiles?.Any() != true)
        {
            return;
        }

        var items = await fileService.LoadMediaItems(selectedFiles);

        var command = new AddMediaItemCommand(playbackService)
        {
            Items = items.ToImmutableArray()
        };

        commandManager.Execute(command);
    }

    private void RemoveAllItems()
    {
        throw new NotImplementedException();
    }
}
