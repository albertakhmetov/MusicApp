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
namespace MusicApp.Controls;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MusicApp.Core.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

public sealed partial class DragTargetControl : UserControl
{
    public static readonly DependencyProperty CanReplaceProperty = DependencyProperty.Register(
        nameof(CanReplace),
        typeof(bool),
        typeof(DragTargetControl),
        null);

    public static readonly DependencyProperty ReplaceCommandProperty = DependencyProperty.Register(
        nameof(ReplaceCommand),
        typeof(ICommand),
        typeof(DragTargetControl),
        null);

    public static readonly DependencyProperty AddCommandProperty = DependencyProperty.Register(
        nameof(AddCommand),
        typeof(ICommand),
        typeof(DragTargetControl),
        null);

    public static readonly DependencyProperty FileServiceProperty = DependencyProperty.Register(
        nameof(FileService),
        typeof(IFileService),
        typeof(DragTargetControl),
        null);

    public DragTargetControl()
    {
        InitializeComponent();
    }

    public event EventHandler? Dropped;

    public bool CanReplace
    {
        get => (bool)GetValue(CanReplaceProperty);
        set => SetValue(CanReplaceProperty, value);
    }

    public ICommand ReplaceCommand
    {
        get => (ICommand)GetValue(ReplaceCommandProperty);
        set => SetValue(ReplaceCommandProperty, value);
    }

    public ICommand AddCommand
    {
        get => (ICommand)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public IFileService FileService
    {
        get => (IFileService)GetValue(FileServiceProperty);
        set => SetValue(FileServiceProperty, value);
    }

    private int ConvertToAddColumn(bool isReplaceVisible)
    {
        return isReplaceVisible ? 2 : 0;
    }

    private int ConvertToAddColumnSpan(bool isReplaceVisible)
    {
        return isReplaceVisible ? 1 : 3;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void OnReplaceDragEnter(object sender, DragEventArgs e)
    {
        VisualStateManager.GoToState(this, "Replace", true);
    }

    private void OnAddDragEnter(object sender, DragEventArgs e)
    {
        VisualStateManager.GoToState(this, "Add", true);
    }

    private async void OnDragOver(object sender, DragEventArgs e)
    {
        var def = e.GetDeferral();

        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var isAccepted = items
                .Select(x => (x as StorageFile)?.Path)
                .Any(x => FileService?.IsSupported(x) == true);

            if (isAccepted)
            {
                e.AcceptedOperation = DataPackageOperation.Link;
                e.DragUIOverride.IsCaptionVisible = false;
                e.DragUIOverride.IsGlyphVisible = false;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }
        finally
        {
            def.Complete();
        }
    }

    private async void OnReplaceDrop(object sender, DragEventArgs e)
    {
        OnDropped();
        ReplaceCommand?.Execute(await GetDroppedFiles(e.DataView));
    }

    private async void OnAddDrop(object sender, DragEventArgs e)
    {
        OnDropped();
        AddCommand?.Execute(await GetDroppedFiles(e.DataView));
    }

    private void OnDropped()
    {
        Dropped?.Invoke(this, EventArgs.Empty);
    }

    private async Task<IList<string>> GetDroppedFiles(DataPackageView dataView)
    {
        if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await dataView.GetStorageItemsAsync();

            return items
                .Select(x => (x as StorageFile)?.Path)
                .Where(x => FileService?.IsSupported(x) == true)
                .ToImmutableArray();
        }

        return ImmutableArray<string>.Empty;
    }
}
