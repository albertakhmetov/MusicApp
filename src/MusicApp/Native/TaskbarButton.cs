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
namespace MusicApp.Native;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicApp.Core;

internal class TaskbarButton : ObservableObject, IDisposable
{
    private static uint IdCounter = 0;

    private bool isEnabled = true, isVisible = true;
    private string? toolTip;
    private SafeHandle? icon;

    public TaskbarButton(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        Id = IdCounter++;
    }

    public uint Id { get; }

    public string Name { get; }

    public bool IsEnabled
    {
        get => isEnabled;
        set => Set(ref isEnabled, value);
    }

    public bool IsVisible
    {
        get => isVisible;
        set => Set(ref isVisible, value);
    }

    public string? ToolTip
    {
        get => toolTip;
        set => Set(ref toolTip, value);
    }

    public SafeHandle? Icon
    {
        get => icon;
        set
        {
            icon?.Dispose();
            icon = value;
            Invalidate(nameof(Icon));
        }
    }

    public ICommand? Command
    {
        get;
        set;
    }

    public object? CommandParameter
    {
        get;
        set;
    }

    public void Dispose()
    {
        icon?.Dispose();
        icon = null;
    }
}
