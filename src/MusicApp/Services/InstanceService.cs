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
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MusicApp.Core.Commands;
using MusicApp.Core.Helpers;
using MusicApp.Core.Services;

internal class InstanceService : IInstanceService, IDisposable
{
    private static readonly string PipeName = $"{IShellService.AppUserModelID}.instance.pipe";

    private readonly IAppCommandManager appCommandManager;
    private readonly IPlaylistStorageService playlistStorageService;
    private readonly ILogger logger;

    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Task listenerTask;

    private readonly Subject<string> incomeFileNames = new();
    private IDisposable? incomeFileSubscription;

    public InstanceService(
        IAppCommandManager appCommandManager,
        IPlaylistStorageService playlistStorageService,
        ILogger<InstanceService> logger)
    {
        ArgumentNullException.ThrowIfNull(appCommandManager);
        ArgumentNullException.ThrowIfNull(playlistStorageService);
        ArgumentNullException.ThrowIfNull(logger);

        this.appCommandManager = appCommandManager;
        this.playlistStorageService = playlistStorageService;
        this.logger = logger;

        cancellationTokenSource = new CancellationTokenSource();
        listenerTask = new Task(async () =>
        {
            while (CancellationToken.IsCancellationRequested is false)
            {
                using var pipeServer = new NamedPipeServerStream(
                    pipeName: PipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                try
                {
                    await pipeServer.WaitForConnectionAsync(CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    using var reader = new StreamReader(pipeServer, Encoding.UTF8);
                    var receivedData = await reader.ReadToEndAsync();

                    foreach (var fileName in DeserializeData(receivedData))
                    {
                        incomeFileNames.OnNext(fileName);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Error during receiving instance data.");

                    throw;
                }
            }
        });
    }

    private CancellationToken CancellationToken => cancellationTokenSource.Token;

    public static async Task SendDataToFirstInstance(string[] args)
    {
        var data = SerializeData(args);

        using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);

        if (await TryToConnect(pipeClient, 5))
        {
            using var writer = new StreamWriter(pipeClient, Encoding.UTF8);
            await writer.WriteAsync(data);
            await writer.FlushAsync();
        }
    }

    private static async Task<bool> TryToConnect(NamedPipeClientStream pipeClient, int maxAttemptCount)
    {
        var attemptNo = 0;
        while (attemptNo < maxAttemptCount)
        {
            try
            {
                await pipeClient.ConnectAsync(1000);
                return true;
            }
            catch (TimeoutException)
            {
                attemptNo++;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return false;
    }

    public async Task StartAsync(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        incomeFileSubscription = incomeFileNames
            .Buffer(incomeFileNames.Throttle(TimeSpan.FromMilliseconds(250)))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(async fileNames => await AddFilesAndActivate(fileNames));

        if (arguments.Any())
        {
            await playlistStorageService.StartAsync(loadPlaylist: false);

            arguments.ForEach(fileName => incomeFileNames.OnNext(fileName));
        }
        else
        {
            await playlistStorageService.StartAsync(loadPlaylist: true);
        }

        listenerTask.Start();
    }

    public void Dispose()
    {
        if (incomeFileSubscription is not null)
        {
            cancellationTokenSource.Cancel();
            incomeFileSubscription?.Dispose();
            incomeFileSubscription = null;
        }
    }

    private static string SerializeData(string[] args) => string.Join(Environment.NewLine, args);

    private static string[] DeserializeData(string value) => value.Split(Environment.NewLine);

    private async Task AddFilesAndActivate(IList<string> fileNames)
    {
        await appCommandManager.ExecuteAsync(new MediaItemAddCommand.Parameters
        {
            Overwrite = false,
            Play = true,
            FileNames = fileNames.ToImmutableArray()
        });
    }
}
