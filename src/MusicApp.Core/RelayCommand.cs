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
namespace MusicApp.Core;

using System.Windows.Input;

public class RelayCommand : ICommand
{
    private readonly Action<object?> action;
    private readonly Func<object?, bool>? canExecute;

    public RelayCommand(Action<object?> action, Func<object?, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        this.action = action;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute == null || canExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        action(parameter);
    }
}
