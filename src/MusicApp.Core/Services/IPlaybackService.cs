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
namespace MusicApp.Core.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Models;

public interface IPlaybackService
{
    IObservable<MediaItem> MediaItem { get; }

    IObservable<ImageData> MediaItemCover { get; }

    ItemCollection<MediaItem> Items { get; }

    IObservable<int> Position { get; }

    IObservable<int> Duration { get; }

    IObservable<int> Volume { get; }

    IObservable<PlaybackState> State { get; }

    IObservable<bool> CanGoPrevious { get; }

    IObservable<bool> CanGoNext { get; }

    IObservable<bool> ShuffleMode { get; }

    IObservable<bool> RepeatMode { get; }

    void Play(MediaItem? mediaItem);
   
    void Play();

    void Pause();

    void TogglePlayback();

    void GoPrevious();

    void GoNext();

    void SetPosition(int position);

    void SetVolume(int volume);

    void SetShuffleMode(bool isShuffleMode);

    void SetRepeatMode(bool isRepeatMode);
}
