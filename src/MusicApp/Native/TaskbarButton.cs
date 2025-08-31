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
using System.Windows.Forms;
using System.Windows.Input;
using MusicApp.Core;

internal sealed class TaskbarButton : IDisposable
{
    private static uint IdCounter = 0;

    private bool isEnabled = true, isVisible = true;

    public TaskbarButton(SafeHandle icon)
    {
        ArgumentNullException.ThrowIfNull(icon);

        isEnabled = true;
        isVisible = true;

        Id = ++IdCounter;
        Icon = icon;
    }

    public uint Id { get; }

    public SafeHandle Icon { get; }

    public required string ToolTip { get; init; }

    public required ICommand Command { get; init; }

    public object? CommandParameter { get; init; }

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled != value)
            {
                isEnabled = value;
                OnEnabledChanged();
            }
        }
    }

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible != value)
            {
                isVisible = value;
                OnVisibleChanged();
            }
        }
    }

    public event EventHandler? EnabledChanged;

    public event EventHandler? VisibleChanged;

    public void Dispose()
    {
        if (Icon.IsClosed is false && Icon.IsInvalid is false)
        {
            Icon.Dispose();
        }
    }

    private void OnEnabledChanged()
    {
        EnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnVisibleChanged()
    {
        VisibleChanged?.Invoke(this, EventArgs.Empty);
    }
}
