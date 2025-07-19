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
namespace MusicApp.Helpers;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MusicApp.Core.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.UI.Shell;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using MusicApp.Core.Helpers;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using System.Reactive.Concurrency;

internal class TaskbarHelper : IDisposable
{
    private const int WM_COMMAND = 0x0111;

    private readonly CompositeDisposable disposable = [];

    private readonly IAppWindow window;
    private readonly List<Button> buttonsList;
    private readonly Subject<Button> buttonChangedSubject, buttonsListChangedSubject;

    private readonly WNDPROC windowProc, nativeWindowProc;

    private readonly Subject<int> commandExecutionSubject;

    private ComObjectSafeHandle<ITaskbarList3> taskbarList;

    public TaskbarHelper(IAppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        this.window = window;

        buttonsList = new List<Button>();
        buttonsListChangedSubject = new Subject<Button>();
        buttonChangedSubject = new Subject<Button>();

        taskbarList = new ComObjectSafeHandle<ITaskbarList3>((ITaskbarList3)new CTaskbarList());
        taskbarList.Value.HrInit();

        windowProc = new WNDPROC(WindowProc);
        var p = PInvoke.SetWindowLongPtr(
            hWnd: (HWND)window.Handle,
            nIndex: WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
            dwNewLong: Marshal.GetFunctionPointerForDelegate<WNDPROC>(windowProc));
        nativeWindowProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(p);

        commandExecutionSubject = new Subject<int>();
        commandExecutionSubject
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(buttonId => ExecuteCommand(buttonId))
            .DisposeWith(disposable);

        InitSubscriptions();
    }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }

        taskbarList?.Dispose();

        foreach (var b in buttonsList)
        {
            b.Dispose();
        }
    }

    public Button? this[string name]
    {
        get => buttonsList.FirstOrDefault(x => x.Name == name);
    }

    public Button AddButton(string name)
    {
        var button = new Button(this, name);
        buttonsList.Add(button);
        buttonsListChangedSubject.OnNext(button);

        return button;
    }

    public bool RemoveButton(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var button = buttonsList.FirstOrDefault(x => x.Name == name);

        if (button != null && buttonsList.Remove(button))
        {
            buttonsListChangedSubject.OnNext(button);
            button.Dispose();

            return true;
        }
        else
        {
            return false;
        }
    }

    private LRESULT WindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM_COMMAND)
        {
            var buttonId = (int)(wParam & 0xFFFF);

            commandExecutionSubject.OnNext(buttonId);
        }

        return PInvoke.CallWindowProc(nativeWindowProc, hWnd, msg, wParam, lParam);
    }

    private void ExecuteCommand(int buttonId)
    {
        var button = buttonsList.FirstOrDefault(button => button.Id == buttonId);

        button?.Command?.Execute(button.CommandParameter);
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        buttonChangedSubject
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(button => UpdateButtons([button]))
            .DisposeWith(disposable);

        buttonsListChangedSubject
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ => AddButtons())
            .DisposeWith(disposable);
    }

    private void AddButtons()
    {
        var hWnd = (HWND)window.Handle;
        var thumbButtons = PrepareThumbButtons(buttonsList);

        taskbarList.Value.ThumbBarAddButtons(hWnd, thumbButtons.AsSpan());
    }

    private unsafe void UpdateButtons(IList<Button> buttons)
    {
        var hWnd = (HWND)window.Handle;
        var thumbButtons = PrepareThumbButtons(buttons);

        taskbarList.Value.ThumbBarUpdateButtons(hWnd, thumbButtons.AsSpan());
    }

    private static THUMBBUTTON[] PrepareThumbButtons(IList<Button> buttons)
    {
        var thumbButtons = new THUMBBUTTON[buttons.Count];

        for (var i = 0; i < thumbButtons.Length; i++)
        {
            thumbButtons[i] = new THUMBBUTTON
            {
                dwMask = THUMBBUTTONMASK.THB_FLAGS | THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP,
                iId = buttons[i].Id,
                dwFlags = (buttons[i].IsEnabled ? THUMBBUTTONFLAGS.THBF_ENABLED : THUMBBUTTONFLAGS.THBF_DISABLED),
                hIcon = (HICON)(buttons[i].Icon?.DangerousGetHandle() ?? nint.Zero),
                szTip = buttons[i].ToolTip ?? string.Empty
            };
        }

        return thumbButtons;
    }

    public class Button : IDisposable
    {
        private static uint IdCounter = 0;

        private readonly TaskbarHelper taskbar;

        private bool isEnabled = true;
        private string? toolTip;
        private SafeHandle? icon;

        public Button(TaskbarHelper taskbar, string name)
        {
            ArgumentNullException.ThrowIfNull(taskbar);
            ArgumentException.ThrowIfNullOrEmpty(name);

            this.taskbar = taskbar;
            Name = name;

            Id = IdCounter++;
        }

        public uint Id { get; }

        public string Name { get; }

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    NotifyChanged();
                }
            }
        }

        public string? ToolTip
        {
            get => toolTip;
            set
            {
                if (toolTip != value)
                {
                    toolTip = value;
                    NotifyChanged();
                }
            }
        }

        public SafeHandle? Icon
        {
            get => icon;
            set
            {
                icon?.Dispose();
                icon = value;
                NotifyChanged();
            }
        }

        public ICommand? Command
        {
            get;
            set;
        }

        public object? CommandParameter
        {
            get;
            set;
        }

        public void Dispose()
        {
            icon?.Dispose();
            icon = null;
        }

        private void NotifyChanged()
        {
            taskbar.buttonChangedSubject.OnNext(this);
        }
    }

    private class ComObjectSafeHandle<T> : SafeHandle where T : class
    {
        public ComObjectSafeHandle(T value) : base(nint.Zero, true)
        {
            ArgumentNullException.ThrowIfNull(value);

            Value = value;
            SetHandle(Marshal.GetIUnknownForObject(value));
        }

        public T Value { get; private set; }

        public override bool IsInvalid => handle == nint.Zero;

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                Marshal.ReleaseComObject(Value);
            }

            return true;
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class CTaskbarList
    {
    }
}
