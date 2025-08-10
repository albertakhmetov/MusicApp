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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Helpers;
using MusicApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.Extensions.Hosting;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Shell;
using Microsoft.Windows.AppLifecycle;
using MusicApp.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;
using MusicApp.Core.Helpers;

internal class AppService : IAppService
{
    private readonly IAppWindow appWindow;

    public AppService(IAppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(appWindow);

        this.appWindow = appWindow;
    }

    public async Task<IList<string>> PickFilesForOpenAsync(IImmutableList<FileType> fileTypes)
    {
        var openPicker = new FileOpenPicker();
        InitializeWithWindow.Initialize(openPicker, appWindow.Handle);

        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        fileTypes.ForEach(x => openPicker.FileTypeFilter.Add(x.Extension));

        var files = await openPicker.PickMultipleFilesAsync();
        return files?.Select(x => x.Path).ToArray() ?? [];
    }

    public async Task<string?> PickFileForOpenAsync(IImmutableList<FileType> fileTypes)
    {
        var openPicker = new FileOpenPicker();
        InitializeWithWindow.Initialize(openPicker, appWindow.Handle);

        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        fileTypes.ForEach(x => openPicker.FileTypeFilter.Add(x.Extension));

        var file = await openPicker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickFileForSaveAsync(IImmutableList<FileType> fileTypes, string? suggestedFileName = null)
    {
        var savePicker = new FileSavePicker();
        InitializeWithWindow.Initialize(savePicker, appWindow.Handle);

        savePicker.SuggestedFileName = suggestedFileName ?? string.Empty;
        savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        foreach (var category in fileTypes.GroupBy(x => x.Description))
        {
            savePicker.FileTypeChoices.Add(category.Key, category.Select(x => x.Extension).ToArray());
        }

        var file = await savePicker.PickSaveFileAsync();
        return file?.Path;
    }
}
