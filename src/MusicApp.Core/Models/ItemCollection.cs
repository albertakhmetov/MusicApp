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

public abstract class ItemCollection<T> : IObservable<ItemCollection<T>.CollectionAction> where T : class, IEquatable<T>
{
    private readonly Subject<CollectionAction> subject = new();

    public abstract IImmutableList<T> Items { get; }

    public abstract int Count { get; }

    public event EventHandler? CollectionChanged;

    public async Task AddAsync(IEnumerable<T> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);

        var filteredItems = await AddRangeAsync(newItems);
        OnCollectionChanged();

        subject.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Add,
            Items = filteredItems,
        });
    }

    public async Task SetAsync(IEnumerable<T> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);

        Clear();

        var filteredItems = await AddRangeAsync(newItems);
        OnCollectionChanged();

        subject.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Reset,
            Items = filteredItems.ToImmutableArray()
        });
    }

    public bool Remove(T? item)
    {
        if (item == null)
        {
            return false;
        }

        var index = IndexOf(item);

        if (index > -1)
        {
            RemoveAt(index);
            OnCollectionChanged();

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
        Clear();
        OnCollectionChanged();

        subject.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Reset,
            Items = []
        });
    }

    public abstract bool Contains(T item);

    public IDisposable Subscribe(IObserver<CollectionAction> observer)
    {
        observer.OnNext(new CollectionAction
        {
            Type = CollectionActionType.Reset,
            Items = Items.ToImmutableArray(),
        });

        return subject.Subscribe(observer);
    }

    protected virtual async Task<IImmutableList<T>> AddRangeAsync(IEnumerable<T> newItems)
    {
        var filteredItems = newItems.Where(x => !Contains(x)).ToImmutableArray();
        foreach (var i in filteredItems)
        {
            await InsertAsync(i);
        }

        return filteredItems;
    }

    protected abstract void Clear();

    protected abstract int IndexOf(T item);

    protected abstract Task InsertAsync(T item, int? index = null);

    protected abstract void RemoveAt(int index);

    protected virtual void OnCollectionChanged()
    {
        CollectionChanged?.Invoke(this, EventArgs.Empty);
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
