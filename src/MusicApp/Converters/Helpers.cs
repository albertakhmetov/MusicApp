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
namespace MusicApp.Converters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MusicApp.Core;
using MusicApp.Core.Models;

static class Helpers
{
    public static bool Not(bool value) => !value;

    public static bool And(bool a, bool b) => a && b;

    public static bool Or(bool a, bool b) => a || b;

    public static ImageSource? LoadCover(ImageData mediaFileCover)
    {
        if (mediaFileCover == null || mediaFileCover.Size == 0)
        {
            return null;
        }

        using var stream = mediaFileCover.GetStream().AsRandomAccessStream();

        var bitmapImage = new BitmapImage();
        bitmapImage.SetSource(stream);

        return bitmapImage;
    }

    public static string ToVolumeIcon(int value)
    {
        if (value == 0)
        {
            return "\uE74F";
        }
        else if (value < 25)
        {
            return "\uE992";
        }
        else if (value < 50)
        {
            return "\uE993";
        }
        else if (value < 75)
        {
            return "\uE994";
        }
        else
        {
            return "\uE995";
        }
    }

    public static string ToTimeString(int time)
    {
        return ToString(TimeSpan.FromSeconds(time));
    }

    public static string ToString(TimeSpan? time)
    {
        if (time == null)
        {
            return string.Empty;
        }
        else if (time.Value.Hours == 0)
        {
            return $"{time.Value:mm\\:ss}";
        }
        else
        {
            return $"{time.Value:hh\\:mm\\:ss}";
        }
    }

    public static Visibility VisibleIf(object value)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIf(bool value)
    {
        return value == true ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIf(int value)
    {
        return value > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIf(double value)
    {
        return VisibleIf(value.ToInt32());
    }

    public static Visibility VisibleIfNot(object value)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIfNot(bool value)
    {
        return value == false ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIfNot(int value)
    {
        return value == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIfNot(double value)
    {
        return VisibleIf(value.ToInt32());
    }
}
