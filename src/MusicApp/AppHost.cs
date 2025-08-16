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
namespace MusicApp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MusicApp.Core;

public class AppHost : IHost, IDisposable
{
    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    private readonly ApplicationLifetime lifetime;
    private ServiceProvider? serviceProvider;
    private IEnumerable<IHostedService>? hostedServices;

    private AppHost(ServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        lifetime = new ApplicationLifetime();
        serviceCollection.AddSingleton<IHostApplicationLifetime>(lifetime);

        serviceProvider = serviceCollection.BuildServiceProvider();
    }

    public IServiceProvider Services => serviceProvider ?? throw new ObjectDisposedException(nameof(AppHost));

    public static IHost Build(IAppEnvironment appEnvironment, Action<IServiceCollection> configureServicesDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureServicesDelegate);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAppEnvironment>(appEnvironment);

        configureServicesDelegate(serviceCollection);

        return new AppHost(serviceCollection);
    }

    public void Dispose()
    {
        serviceProvider?.Dispose();
        serviceProvider = null;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var logger = Services.GetRequiredService<ILogger<AppHost>>();
        try
        {
            XamlCheckProcessRequirements();
            WinRT.ComWrappersSupport.InitializeComWrappers();

            hostedServices = Services.GetRequiredService<IEnumerable<IHostedService>>();

            foreach (var service in hostedServices)
            {
                await service.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            lifetime.NotifyStarted();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unable to initialize the application");
        }

        try
        {
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);

                var app = Services.GetRequiredService<App>();
                app.UnhandledException += (_, e) => logger.LogCritical(e.Exception, "Unhandled Exception");
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unable to start the application");
        }

        lifetime.StopApplication();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lifetime.NotifyStopping();

        if (hostedServices is not null)
        {
            foreach (var service in hostedServices.Reverse())
            {
                await service.StopAsync(cancellationToken);
            }
        }

        lifetime.NotifyStopped();
    }

    private sealed class ApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource startedSource = new();
        private readonly CancellationTokenSource stoppingSource = new();
        private readonly CancellationTokenSource stoppedSource = new();

        private bool isDisposed = false;

        public CancellationToken ApplicationStarted => startedSource.Token;

        public CancellationToken ApplicationStopping => stoppingSource.Token;

        public CancellationToken ApplicationStopped => stoppedSource.Token;

        public void StopApplication()
        {
            Application.Current?.Exit();
        }

        public void Dispose()
        {
            if (isDisposed is false)
            {
                startedSource.Dispose();
                stoppingSource.Dispose();
                stoppedSource.Dispose();

                isDisposed = true;
            }
        }

        public void NotifyStarted() => startedSource.Cancel();

        public void NotifyStopping() => stoppingSource.Cancel();

        public void NotifyStopped() => stoppedSource.Cancel();
    }
}
