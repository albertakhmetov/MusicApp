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
namespace MusicApp.Helpers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

public class VisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return Converters.VisibleIf(b);
        }
        else if (value is int i)
        {
            return Converters.VisibleIf(i);
        }
        else if (value is double d)
        {
            return Converters.VisibleIf(d);
        }
        else
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
