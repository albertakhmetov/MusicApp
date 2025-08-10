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
namespace MusicApp.Services;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MusicApp.Core;
using MusicApp.Core.Commands;
using MusicApp.Core.Helpers;
using MusicApp.Core.Services;
using MusicApp.Native;

internal class SingleInstanceService : ISingleInstanceService
{
    private readonly CompositeDisposable disposable = [];

    private readonly IFileService fileService;
    private readonly IAppCommandManager appCommandManager;
    private readonly SingleInstance singleInstance;

    public SingleInstanceService(
        IFileService fileService,
        IAppCommandManager appCommandManager,
        [FromKeyedServices("Main")] IAppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(appCommandManager);
        ArgumentNullException.ThrowIfNull(appWindow);

        this.fileService = fileService;
        this.appCommandManager = appCommandManager;

        singleInstance = new SingleInstance(appWindow);
        
        InitSubscriptions();
    }

    public void Dispose()
    {
        if (disposable?.IsDisposed is false)
        {
            disposable.Dispose();
        }

        singleInstance.Dispose();
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current is null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        singleInstance
            .DataReceived
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(data => AddItems(data))
            .DisposeWith(disposable);
    }

    private async void AddItems(string data)
    {
        var fileNames = data.Split(Environment.NewLine);

        var items = await fileService.LoadMediaItems(fileNames);

        await appCommandManager.ExecuteAsync(new MediaItemAddCommand.Parameters
        {
            Overwrite = false,
            Items = items.ToImmutableArray()
        });
    }
}
