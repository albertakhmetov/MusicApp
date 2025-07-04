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
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

public class UndoableHistoryManager : IUndoableHistoryManager
{
    private readonly UndoableHistory undoHistory = [], redoHistory = [];
    private readonly BehaviorSubject<int> executedCountSubject;

    public UndoableHistoryManager()
    {
        executedCountSubject = new BehaviorSubject<int>(0);

        CanUndo = undoHistory.Select((bool x) => !x).AsObservable();
        CanRedo = redoHistory.Select((bool x) => !x).AsObservable();
        ExecutedCount = executedCountSubject.AsObservable();
    }

    public IObservable<bool> CanUndo { get; }

    public IObservable<bool> CanRedo { get; }

    public IObservable<int> ExecutedCount { get; }

    public void Push(IUndoable undoable)
    {
        ArgumentNullException.ThrowIfNull(undoable);

        if (undoable.IsExecuted is false)
        {
            throw new InvalidOperationException("Can't add a not executed commmand");
        }

        undoHistory.Push(undoable);
        redoHistory.Clear();

        IncreaseExecutedCount();
    }

    public void Clear()
    {
        undoHistory.Clear();
        redoHistory.Clear();

        ResetExecutedCount();
    }

    public void Undo()
    {
        if (undoHistory.TryPop(out var undoableCommand))
        {
            undoableCommand.Undo();
            redoHistory.Push(undoableCommand);

            DecreaseExecutedCount();
        }
    }

    public void Redo()
    {
        if (redoHistory.TryPop(out var undoableCommand))
        {
            undoableCommand.Redo();
            undoHistory.Push(undoableCommand);

            IncreaseExecutedCount();
        }
    }

    private void DecreaseExecutedCount()
    {
        var count = executedCountSubject.Value - 1;

        executedCountSubject.OnNext(count);
    }

    private void IncreaseExecutedCount()
    {
        var count = executedCountSubject.Value + 1;

        executedCountSubject.OnNext(count);
    }

    private void ResetExecutedCount()
    {
        executedCountSubject.OnNext(0);
    }
}
