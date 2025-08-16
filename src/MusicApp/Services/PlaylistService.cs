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

using Microsoft.Extensions.Hosting;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

internal class PlaylistService : IPlaylistService, IDisposable
{
    private const string PLAYLIST_FILENAME = "playlist.json";

    private readonly CompositeDisposable disposable = [];
    private readonly IAppEnvironment appEnvironment;
    private readonly IPlaybackService playbackService;
    private readonly IMetadataService metadataService;

    public PlaylistService(
        IAppEnvironment appEnvironment,
        IPlaybackService playbackService,
        IMetadataService metadataService)
    {
        ArgumentNullException.ThrowIfNull(appEnvironment);
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(metadataService);

        this.appEnvironment = appEnvironment;
        this.playbackService = playbackService;
        this.metadataService = metadataService;
    }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }
    }

    public async Task StartAsync(bool loadPlaylist = true)
    {
        if (loadPlaylist)
        {
            await LoadPlaylist();
        }

        playbackService
            .Items
            .CombineLatest(playbackService.ShuffledItems, playbackService.MediaItem, playbackService.ShuffleMode, playbackService.RepeatMode)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x => SavePlaylist(playbackService.Items.List, x.Second, x.Third, x.Fourth, x.Fifth))
            .DisposeWith(disposable);
    }

    private async Task LoadPlaylist()
    {
        using var stream = appEnvironment
            .UserDataDirectoryInfo
            .GetFileInfo(PLAYLIST_FILENAME)
            .OpenRead();

        if (stream == null)
        {
            return;
        }

        var stateLoader = await PlaylistLoader.Load(metadataService, stream);

        await playbackService.Items.SetAsync(stateLoader.Items ?? []);

        playbackService.SetMediaItem(stateLoader.CurrentItem);
        playbackService.SetRepeatMode(stateLoader.RepeatMode);

        if (stateLoader.ShuffledItems?.Any() == true)
        {
            playbackService.SetShuffleMode(stateLoader.ShuffledItems);
        }
        else
        {
            playbackService.SetShuffleMode(stateLoader.ShuffleMode);
        }
    }

    private void SavePlaylist(
        IImmutableList<MediaItem> items,
        IImmutableList<MediaItem> shuffledItems,
        MediaItem currentItem,
        bool shuffleMode,
        bool repeatMode)
    {
        using var stream = appEnvironment
            .UserDataDirectoryInfo
            .GetFileInfo(PLAYLIST_FILENAME)
            .OpenWrite(overwrite: true);

        var options = new JsonWriterOptions { Indented = true };
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartObject();

        writer.WriteStartArray(nameof(PlaylistLoader.Items));
        items.ForEach(x => writer.WriteStringValue(x.FileName));
        writer.WriteEndArray();

        writer.WriteStartArray(nameof(PlaylistLoader.ShuffledItems));
        shuffledItems.ForEach(x => writer.WriteNumberValue(items.IndexOf(x)));
        writer.WriteEndArray();

        writer.WriteNumber(nameof(PlaylistLoader.CurrentItem), items.IndexOf(currentItem));
        writer.WriteBoolean(nameof(PlaylistLoader.ShuffleMode), shuffleMode);
        writer.WriteBoolean(nameof(PlaylistLoader.RepeatMode), repeatMode);

        writer.WriteEndObject();
    }

    private class PlaylistLoader
    {
        private readonly IMetadataService metadataService;
        private JsonNode? node;

        public static async Task<PlaylistLoader> Load(IMetadataService metadataService, Stream stream)
        {
            var loader = new PlaylistLoader(metadataService);

            await loader.Load(stream);

            return loader;
        }

        private PlaylistLoader(IMetadataService metadataService)
        {
            ArgumentNullException.ThrowIfNull(metadataService);

            this.metadataService = metadataService;
        }

        public IList<MediaItem>? Items { get; private set; }

        public IList<MediaItem>? ShuffledItems { get; private set; }

        public MediaItem? CurrentItem { get; private set; }

        public bool ShuffleMode { get; private set; }

        public bool RepeatMode { get; private set; }

        private async Task Load(Stream stream)
        {
            try
            {
                node = await JsonNode.ParseAsync(stream);

                Items = await metadataService.LoadMediaItemsAsync(GetFileNames(node));
                ShuffledItems = GetShuffledItems(node);

                CurrentItem = GetCurrentItem(node);
                ShuffleMode = GetBooleanValue(node, nameof(ShuffleMode), false);
                RepeatMode = GetBooleanValue(node, nameof(RepeatMode), false);
            }
            catch (JsonException)
            {
            }
        }

        private static bool GetBooleanValue(JsonNode? node, string propertyName, bool defaultValue)
        {
            switch (node?[propertyName]?.GetValueKind())
            {
                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                default:
                    return defaultValue;
            }
        }

        private IList<string> GetFileNames(JsonNode? node)
        {
            if (node?[nameof(Items)] is JsonArray items
                && items.All(x => x?.GetValueKind() == JsonValueKind.String))
            {
                return items.Select(x => x!.GetValue<string>()).ToArray();
            }

            return [];
        }

        private IList<MediaItem> GetShuffledItems(JsonNode? node)
        {
            if (Items?.Any() == true
                && node?[nameof(ShuffledItems)] is JsonArray shuffledItems
                && shuffledItems.All(x => x?.GetValueKind() == JsonValueKind.Number))
            {
                var indices = shuffledItems.Select(x => x!.GetValue<int>()).ToArray();

                if (indices.Any() && indices.Min() >= 0 && indices.Max() < Items.Count)
                {
                    return indices.Select(x => Items[x]).ToImmutableArray();
                }
            }

            return [];
        }

        private MediaItem? GetCurrentItem(JsonNode? node)
        {
            if (Items?.Any() == true && node?[nameof(CurrentItem)]?.GetValueKind() == JsonValueKind.Number)
            {
                var itemIndex = node[nameof(CurrentItem)]!.GetValue<int>();

                if (itemIndex >= 0 && itemIndex < Items.Count)
                {
                    return Items[itemIndex];
                }
            }

            return null;
        }
    }
}
