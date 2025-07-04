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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

internal class UndoableHistory : IEnumerable<IUndoable>, IObservable<bool>
{
    private readonly LinkedList<IUndoable> items = [];
    private readonly Subject<bool> isEmptySubject = new Subject<bool>();

    public int? MaxSize { get; init; } = 10;

    public int Count => items.Count;

    public IDisposable Subscribe(IObserver<bool> observer)
    {
        return isEmptySubject.StartWith(items.Count == 0).AsObservable().Subscribe(observer);
    }

    public IUndoable Pop()
    {
        if (TryPop(out var undoable))
        {
            return undoable!;
        }
        else
        {
            throw new InvalidOperationException("The history is empty");
        }
    }

    public bool TryPop([MaybeNullWhen(false)] out IUndoable undoable)
    {
        if (items.Count > 0)
        {
            undoable = items.Last();
            items.RemoveLast();
            isEmptySubject.OnNext(items.Count == 0);
            return true;
        }
        else
        {
            undoable = null;
            return false;
        }
    }

    public IUndoable Peek()
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("The history is empty");
        }

        return items.Last();
    }

    public void Push(IUndoable undoable)
    {
        ArgumentNullException.ThrowIfNull(undoable);

        if (MaxSize is not null)
        {
            while (Count >= MaxSize)
            {
                if (items.First?.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                items.RemoveFirst();
            }
        }

        items.AddLast(undoable);
        isEmptySubject.OnNext(items.Count == 0);
    }

    public void Clear()
    {
        foreach (var i in items)
        {
            if (i is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        items.Clear();
        isEmptySubject.OnNext(items.Count == 0);
    }

    public IEnumerator<IUndoable> GetEnumerator()
    {
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
