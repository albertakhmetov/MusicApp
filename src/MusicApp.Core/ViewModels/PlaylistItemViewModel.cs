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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicApp.Core.Models;

public sealed class PlaylistItemViewModel : ViewModel
{
    private readonly PlaylistViewModel playlist;

    public PlaylistItemViewModel(PlaylistViewModel playlist, MediaItem mediaItem)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(mediaItem);

        this.playlist = playlist;

        MediaItem = mediaItem;
    }

    public MediaItem MediaItem { get; }

    public bool IsCurrent => playlist.CurrentItem?.Equals(MediaItem) == true;

    public ICommand PlayCommand => playlist.PlayCommand;

    public ICommand RemoveCommand => playlist.RemoveCommand;
}
