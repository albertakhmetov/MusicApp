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
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Helpers;
using MusicApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Windows.Services.Maps;

internal class SettingsService : ISettingsService, IDisposable
{
    private const string SETTINGS_FILENAME = "settings.json";

    private readonly CompositeDisposable disposable = [];
    private readonly BehaviorSubject<bool> changedSubject;
    private readonly IFileService fileService;

    public SettingsService(IFileService fileService)
    {
        ArgumentNullException.ThrowIfNull(fileService);

        this.fileService = fileService;

        WindowTheme = new SettingsProperty<WindowTheme>(Core.Models.WindowTheme.System);

        LoadSettings();

        changedSubject = new BehaviorSubject<bool>(false);
        changedSubject
            .DistinctUntilChanged()
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Where(x => x is true)
            .Subscribe(_ => SaveSettings())
            .DisposeWith(disposable);

        WindowTheme
            .DistinctUntilChanged()
            .Skip(1)
            .Select(_ => true)
            .Subscribe(changedSubject)
            .DisposeWith(disposable);
    }

    public SettingsProperty<WindowTheme> WindowTheme { get; }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }
    }

    private void LoadSettings()
    {
        using var stream = fileService.ReadUserFile(SETTINGS_FILENAME);

        if (stream is null)
        {
            return;
        }

        try
        {
            var node = JsonNode.Parse(stream);

            var windowThemeNode = node?[nameof(ISettingsService.WindowTheme)];
            if (windowThemeNode?.GetValueKind() == JsonValueKind.String
                && Enum.TryParse<WindowTheme>(windowThemeNode.GetValue<string>(), out var windowTheme))
            {
                WindowTheme.Value = windowTheme;
            }
        }
        catch (JsonException)
        {
        }
    }

    private void SaveSettings()
    {
        using var stream = fileService.WriteUserFile(SETTINGS_FILENAME, overwrite: true);

        var windowTheme = WindowTheme.Value;

        var options = new JsonWriterOptions { Indented = true };
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartObject();

        writer.WriteString(nameof(ISettingsService.WindowTheme), windowTheme.ToString());

        writer.WriteEndObject();

        changedSubject.OnNext(false);
    }
}
