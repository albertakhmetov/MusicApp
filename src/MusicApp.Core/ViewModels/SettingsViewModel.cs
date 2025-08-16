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
namespace MusicApp.Core.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;

public class SettingsViewModel : ViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly ISettingsService settingsService;
    private readonly IShellService shellService;

    private WindowTheme windowTheme;
    private bool isAppRegistred;

    public SettingsViewModel(ISettingsService settingsService, IShellService shellService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(shellService);

        this.settingsService = settingsService;
        this.shellService = shellService;

        WindowThemes = [WindowTheme.System, WindowTheme.Dark, WindowTheme.Light];

        InitSubscriptions();
    }

    public WindowTheme WindowTheme
    {
        get => windowTheme;
        set => settingsService.WindowTheme.Value = value;
    }

    public bool IsAppRegistred
    {
        get => isAppRegistred;
        set
        {
            if (value)
            {
                shellService.Register();
            }
            else
            {
                shellService.Unregister();
            }
        }
    }

    public IImmutableList<WindowTheme> WindowThemes { get; }

    public string ProductName => shellService.Info.ProductName;

    public string Version => shellService.Info.FileVersion.ToString(3);

    public string LegalCopyright => shellService.Info.LegalCopyright ?? string.Empty;

    public string ProductDescription => shellService.Info.ProductDescription ?? string.Empty;

    public string ProductVersion => string.IsNullOrEmpty(shellService.Info.ProductVersion) 
        ? string.Empty 
        : $"Build {shellService.Info.ProductVersion}";

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        settingsService
            .WindowTheme
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x =>
            {
                windowTheme = x;
                Invalidate(nameof(WindowTheme));
            })
            .DisposeWith(disposable);

        shellService
            .IsRegistred
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(x =>
            {
                isAppRegistred = x;
                Invalidate(nameof(IsAppRegistred));
            })
            .DisposeWith(disposable);
    }
}
