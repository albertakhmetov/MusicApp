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
namespace MusicApp.Native;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

internal sealed class Taskbar : IDisposable
{
    private const int WM_COMMAND = 0x0111;

    private readonly CompositeDisposable disposable = [];

    private readonly IAppWindow appWindow;
    private readonly List<TaskbarButton> buttonsList;
    private readonly Subject<TaskbarButton> buttonChangedSubject, buttonsListChangedSubject;

    private readonly Receiver receiver;

    private ComObjectSafeHandle<ITaskbarList3> taskbarList;

    public Taskbar(IAppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(appWindow);
        ArgumentNullException.ThrowIfNull(appWindow.Procedure);

        this.appWindow = appWindow;

        buttonsList = new List<TaskbarButton>();
        buttonsListChangedSubject = new Subject<TaskbarButton>();
        buttonChangedSubject = new Subject<TaskbarButton>();

        taskbarList = new ComObjectSafeHandle<ITaskbarList3>((ITaskbarList3)new CTaskbarList());
        taskbarList.Value.HrInit();

        receiver = new Receiver();
        receiver.ButtonClicked
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(buttonId => ExecuteCommand(buttonId))
            .DisposeWith(disposable);


        InitSubscriptions();

        this.appWindow.Procedure
            .Subscribe(WM_COMMAND, receiver)
            .DisposeWith(disposable);
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

    public TaskbarButton? GetButton(string name) => buttonsList.FirstOrDefault(x => x.Name == name);

    public TaskbarButton AddButton(string name)
    {
        var button = new TaskbarButton(name);
        button.PropertyChanged += OnButtonPropertyChanged;

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
            button.PropertyChanged -= OnButtonPropertyChanged;

            buttonsListChangedSubject.OnNext(button);
            button.Dispose();

            return true;
        }
        else
        {
            return false;
        }
    }

    private void OnButtonPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TaskbarButton button)
        {
            buttonChangedSubject.OnNext(button);
        }
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
        var thumbButtons = PrepareThumbButtons(buttonsList);

        taskbarList.Value.ThumbBarAddButtons((HWND)appWindow.Handle, thumbButtons.AsSpan());
    }

    private unsafe void UpdateButtons(IList<TaskbarButton> buttons)
    {
        var thumbButtons = PrepareThumbButtons(buttons);

        taskbarList.Value.ThumbBarUpdateButtons((HWND)appWindow.Handle, thumbButtons.AsSpan());
    }

    private static THUMBBUTTON[] PrepareThumbButtons(IList<TaskbarButton> buttons)
    {
        var thumbButtons = new THUMBBUTTON[buttons.Count];

        for (var i = 0; i < thumbButtons.Length; i++)
        {
            thumbButtons[i] = new THUMBBUTTON
            {
                dwMask = THUMBBUTTONMASK.THB_FLAGS | THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP,
                iId = buttons[i].Id,
                dwFlags = buttons[i].IsEnabled ? THUMBBUTTONFLAGS.THBF_ENABLED : THUMBBUTTONFLAGS.THBF_DISABLED,
                hIcon = (HICON)(buttons[i].Icon?.DangerousGetHandle() ?? nint.Zero),
                szTip = buttons[i].ToolTip ?? string.Empty
            };
        }

        return thumbButtons;
    }

    private sealed class Receiver : IAppWindowProcedure.IReceiver
    {
        private readonly Subject<int> buttonClickedSubject;

        public Receiver()
        {
            buttonClickedSubject = new Subject<int>();
            ButtonClicked = buttonClickedSubject.AsObservable();
        }

        public IObservable<int> ButtonClicked { get; }

        public bool Process(uint message, nuint wParam, nint lParam)
        {
            if (message != WM_COMMAND)
            {
                return false;
            }

            var buttonId = (int)(wParam & 0xFFFF);
            buttonClickedSubject.OnNext(buttonId);

            return true;
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
