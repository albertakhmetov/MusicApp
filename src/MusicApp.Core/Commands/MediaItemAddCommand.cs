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
namespace MusicApp.Core.Commands;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;

public class MediaItemAddCommand : IAppCommand<MediaItemAddCommand.Parameters>
{
    private readonly IMetadataService metadataService;
    private readonly IPlaybackService playbackService;

    public MediaItemAddCommand(IMetadataService metadataService, IPlaybackService playbackService)
    {
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(playbackService);

        this.metadataService = metadataService;
        this.playbackService = playbackService;
    }

    public async Task ExecuteAsync(Parameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.FileNames?.Any() != true)
        {
            return;
        }

        var fileNames = parameters.FileNames.OrderBy(x => Path.GetFileName(x), StringLogicalComparer.Instance);

        var mediaItems = await metadataService.LoadMediaItemsAsync(fileNames);

        if (parameters.Overwrite)
        {
            await playbackService.Items.SetAsync(mediaItems);
        }
        else
        {
            await playbackService.Items.AddAsync(mediaItems);
        }

        if (parameters.Play)
        {
            playbackService.Play(mediaItems.FirstOrDefault());
        }
    }

    public sealed class Parameters
    {
        public required IImmutableList<string> FileNames { get; init; }

        public bool Overwrite { get; init; } = false;

        public bool Play { get; init; } = false;
    }
}
