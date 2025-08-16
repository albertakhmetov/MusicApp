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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MusicApp.Core;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;

internal class WindowService : IWindowService
{
    private readonly Dictionary<string, IServiceScope> scopes = [];
    private readonly IServiceProvider serviceProvider;

    public WindowService(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        this.serviceProvider = serviceProvider;
    }

    public async Task<IAppWindow> GetWindowAsync<T>() where T : ViewModel
    {
        var viewModelName = typeof(T).Name;

        if (scopes.TryGetValue(viewModelName, out var scope) is false)
        {
            scope = serviceProvider.CreateScope();

            var window = scope.ServiceProvider.GetRequiredKeyedService<Window>(viewModelName);
            window.Closed += OnWindowClosed;

            var scopeData = scope.ServiceProvider.GetRequiredService<ScopeDataService>();
            scopeData.Init((IAppWindow)window);

            scopes.Add(viewModelName, scope);

            var view = scope.ServiceProvider.GetRequiredKeyedService<UserControl>(viewModelName);

            if (window.Content is Grid grid)
            {
                grid.Children.Insert(1, view);

                if (window.AppWindow.Presenter is OverlappedPresenter presenter && presenter.HasTitleBar)
                {
                    Grid.SetRow(view, 1);
                }
            }
            else
            {
                window.Content = view;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        return scope.ServiceProvider.GetRequiredService<ScopeDataService>().Window;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        const string windowSuffix = "Window";
        var name = sender?.GetType().Name;

        if (sender is Window window && name?.EndsWith(windowSuffix) is true)
        {
            var viewModelName = $"{name[..^windowSuffix.Length]}ViewModel";

            if (scopes.TryGetValue(viewModelName, out var scope))
            {
                window.Content = null;
                window.Closed -= OnWindowClosed;

                scope.Dispose();

                scopes.Remove(viewModelName);
            }
        }
    }
}
