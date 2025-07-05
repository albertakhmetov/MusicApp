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
namespace MusicApp.Service;

using Microsoft.Extensions.Hosting;
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

internal class KeeperService : IHostedService
{
    private const string PLAYLIST_FILENAME = "playlist.json";
    private const string SETTINGS_FILENAME = "settings.json";

    private readonly CompositeDisposable disposable = [];
    private readonly IPlaybackService playbackService;
    private readonly ISettingsService settingsService;
    private readonly IFileService fileService;

    public KeeperService(IPlaybackService playbackService, ISettingsService settingsService, IFileService fileService)
    {
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(fileService);

        this.playbackService = playbackService;
        this.settingsService = settingsService;
        this.fileService = fileService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LoadSettings();

        await LoadPlaylist();

        playbackService
            .Items
            .CombineLatest(playbackService.ShuffledItems, playbackService.MediaItem, playbackService.ShuffleMode, playbackService.RepeatMode)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(x => SavePlaylist(playbackService.Items.List, x.Second, x.Third, x.Fourth, x.Fifth))
            .DisposeWith(disposable);

        settingsService.WindowTheme
            .DistinctUntilChanged()
            .Select(x => true)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Skip(1)
            .Subscribe(_ => SaveSettings())
            .DisposeWith(disposable);
    }

    private async Task LoadPlaylist()
    {
        using var stream = fileService.ReadUserFile(PLAYLIST_FILENAME);

        if (stream == null)
        {
            return;
        }

        var stateLoader = await PlaylistLoader.Load(fileService, stream);

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
        using var stream = fileService.WriteUserFile(PLAYLIST_FILENAME, overwrite: true);

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

    private void LoadSettings()
    {
        using var stream = fileService.ReadUserFile(SETTINGS_FILENAME);

        if (stream is null)
        {
            return;
        }

        try
        {
            var node = JsonNode.Parse(stream);

            var windowThemeNode = node?[nameof(ISettingsService.WindowTheme)];
            if (windowThemeNode?.GetValueKind() == JsonValueKind.String
                && Enum.TryParse<WindowTheme>(windowThemeNode.GetValue<string>(), out var windowTheme))
            {
                settingsService.WindowTheme.Value = windowTheme;
            }
        }
        catch (JsonException)
        {
        }
    }

    private void SaveSettings()
    {
        using var stream = fileService.WriteUserFile(SETTINGS_FILENAME, overwrite: true);

        var windowTheme = settingsService.WindowTheme.Value;

        var options = new JsonWriterOptions { Indented = true };
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartObject();

        writer.WriteString(nameof(ISettingsService.WindowTheme), windowTheme.ToString());

        writer.WriteEndObject();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!disposable.IsDisposed)
        {
            disposable.Dispose();
        }

        return Task.CompletedTask;
    }

    private class PlaylistLoader
    {
        private readonly IFileService fileService;
        private JsonNode? node;

        public static async Task<PlaylistLoader> Load(IFileService fileService, Stream stream)
        {
            var loader = new PlaylistLoader(fileService);

            await loader.Load(stream);

            return loader;
        }

        private PlaylistLoader(IFileService fileService)
        {
            ArgumentNullException.ThrowIfNull(fileService);

            this.fileService = fileService;
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

                Items = await fileService.LoadMediaItems(GetFileNames(node));
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
