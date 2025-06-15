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

public class AddMediaItemCommand : IAppCommand
{
    private readonly IPlaybackService playbackService;

    public AddMediaItemCommand(IPlaybackService playbackService)
    {
        ArgumentNullException.ThrowIfNull(playbackService);

        this.playbackService = playbackService;
    }

    public required IImmutableList<MediaItem> Items { get; init; }

    public bool Overwrite { get; init; }

    public void Execute()
    {
        if (Items?.Any() != true)
        {
            return;
        }

        var items = Items
            .OrderBy(x => Path.GetFileName(x.FileName), StringLogicalComparer.Instance);

        if (Overwrite)
        {
            playbackService.Items.Set(items);
        }
        else
        {
            playbackService.Items.Add(items);
        }
    }
}
