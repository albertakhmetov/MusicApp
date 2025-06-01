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

using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MusicApp.Core;
using Windows.Win32;

[TemplatePart(Name = "PART_BORDER", Type = typeof(Border))]
[TemplatePart(Name = "PART_SELECTED", Type = typeof(Border))]
public class AppItemContainer : ContentControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(AppItemContainer),
        new PropertyMetadata(null, null));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(AppItemContainer),
        new PropertyMetadata(null, null));

    public static readonly DependencyProperty DoubleClickModeProperty = DependencyProperty.Register(
        nameof(DoubleClickMode),
        typeof(bool),
        typeof(AppItemContainer),
        new PropertyMetadata(null, null));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(AppItemContainer),
        new PropertyMetadata(false, null));

    private CompositeDisposable? disposable;
    private uint lastReleaseTickCount;
    private bool isHovered, isPressed;

    public AppItemContainer()
    {
        DefaultStyleKey = typeof(AppItemContainer);
    }

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool DoubleClickMode
    {
        get => (bool)GetValue(DoubleClickModeProperty);
        set => SetValue(DoubleClickModeProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsHovered
    {
        get => isHovered;
        private set
        {
            if (isHovered != value)
            {
                isHovered = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
        IsHovered = true;

        isPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

        base.OnPointerEntered(e);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
        IsHovered = false;

        base.OnPointerExited(e);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Pressed", true);

        isPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

        base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);

        base.OnPointerReleased(e);

        if (isPressed is not true)
        {
            return;
        }

        var tickCount = PInvoke.GetTickCount();

        if (!e.Handled && (!DoubleClickMode || tickCount - lastReleaseTickCount <= PInvoke.GetDoubleClickTime()))
        {
            Command?.Execute(CommandParameter);
        }

        lastReleaseTickCount = tickCount;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
