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
using MusicApp.Core.Models;
using MusicApp.Core.Services;

public class MediaItemRemoveCommand : IAppCommand<MediaItemRemoveCommand.Parameters>
{
    private readonly IPlaybackService playbackService;

    public MediaItemRemoveCommand(IPlaybackService playbackService)
    {
        ArgumentNullException.ThrowIfNull(playbackService);

        this.playbackService = playbackService;
    }

    public MediaItem? Item { get; init; }

    public Task ExecuteAsync(Parameters parameters)
    {
        if (parameters.Item is null && parameters.RemoveAll is false)
        {
            return Task.FromResult(false);
        }

        if (parameters.RemoveAll)
        {
            playbackService.Items.RemoveAll();
        }
        else
        {
            playbackService.Items.Remove(Item);
        }

        return Task.CompletedTask;
    }

    public sealed class Parameters
    {
        public MediaItem? Item { get; init; }

        public bool RemoveAll { get; init; } = false;
    }
}
