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
namespace MusicApp.Core.Models;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

public class ItemCollection<T> : IObservable<ItemCollection<T>.CollectionAction>
{
    private readonly List<T> items = [];
    private readonly Subject<CollectionAction> subject = new();

    public void Add(IEnumerable<T> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);

        items.AddRange(newItems);
        subject.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Add,
            Items = newItems.ToImmutableArray()
        });
    }

    public void Set(IEnumerable<T> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);

        items.Clear();
        items.AddRange(newItems);

        subject.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Reset,
            Items = newItems.ToImmutableArray()
        });
    }

    public bool Remove(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = items.IndexOf(item);

        if (index > -1)
        {
            items.RemoveAt(index);

            subject.OnNext(new CollectionAction
            {
                Type = CollectionActionType.Remove,
                StartingIndex = index,
                Items = new[] { item }.ToImmutableArray()
            });

            return true;
        }
        else
        {
            return false;
        }
    }

    public void RemoveAll()
    {
        items.Clear();

        subject.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Reset,
            Items = []
        });
    }

    public IDisposable Subscribe(IObserver<CollectionAction> observer)
    {
        observer.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Reset,
            Items = items.ToImmutableArray()
        });

        return subject.Subscribe(observer);
    }

    public sealed class CollectionAction
    {
        public CollectionActionType Type { get; init; }

        public int StartingIndex { get; init; }

        public required IImmutableList<T> Items { get; init; }
    }

    public enum CollectionActionType
    {
        Reset,
        Add,
        Remove,
    }
}
