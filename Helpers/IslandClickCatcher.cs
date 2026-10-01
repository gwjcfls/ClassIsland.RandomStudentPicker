using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core.Assists;

namespace ClassIsland.RandomStudentPicker.Helpers;

/// <summary>
/// 通过低级鼠标钩子（WH_MOUSE_LL）捕捉落在主界面组件上的真实鼠标点击。
/// </summary>
/// <remarks>
/// ClassIsland 主界面窗口在非编辑模式下带有 <c>WS_EX_LAYERED | WS_EX_TRANSPARENT</c>，
/// 鼠标消息会被系统直接派发给下层窗口，主界面里的控件（包括 Avalonia 的按钮）永远收不到点击事件。
/// 本类不去修改主界面的窗口样式，而是从系统层面截获鼠标左键按下消息：如果按下的位置落在某个
/// 已注册的组件区域内，就触发对应回调并把这条消息吞掉（返回 1），这样既能让按钮「可用」，
/// 又保留了 ClassIsland 原有的点击穿透行为。
/// </remarks>
public static class IslandClickCatcher
{
    private const int WhMouseLl = 14;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;

    /// <summary>钩子是否已经安装。</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>最近一次错误信息，便于在日志中排查。</summary>
    public static string? LastError { get; private set; }

    private sealed record Entry(WeakReference<Control> Anchor, Func<Rect?> HitArea, Action OnClick, Action? OnRightClick);

    private static readonly object Sync = new();
    private static readonly List<Entry> Entries = [];

    private static HookProc? _hookProc;      // 必须持有引用，否则会被 GC 回收
    private static nint _hookHandle;

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public Point32 Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandle(string? lpModuleName);

    /// <summary>注册一个可点击区域。</summary>
    /// <param name="anchor">用于判断组件是否仍然存活/可见的控件。</param>
    /// <param name="hitArea">返回组件当前的屏幕矩形（物理像素）；null 表示当前不可点击。</param>
    /// <param name="onClick">左键点击回调。</param>
    /// <param name="onRightClick">右键点击回调（可选）。</param>
    /// <returns>用于注销的句柄。</returns>
    public static IDisposable Register(Control anchor, Func<Rect?> hitArea, Action onClick, Action? onRightClick = null)
    {
        var entry = new Entry(new WeakReference<Control>(anchor), hitArea, onClick, onRightClick);

        lock (Sync)
        {
            Entries.RemoveAll(x => !x.Anchor.TryGetTarget(out _));
            Entries.Add(entry);
        }

        Start();
        return new Registration(entry);
    }

    private sealed class Registration(Entry entry) : IDisposable
    {
        public void Dispose()
        {
            lock (Sync)
            {
                Entries.Remove(entry);
            }

            if (Entries.Count == 0)
            {
                Stop();
            }
        }
    }

    /// <summary>安装鼠标钩子。</summary>
    public static void Start()
    {
        if (!OperatingSystem.IsWindows() || IsRunning)
        {
            return;
        }

        try
        {
            _hookProc = HookCallback;
            _hookHandle = SetWindowsHookEx(WhMouseLl, _hookProc, GetModuleHandle(null), 0);
            if (_hookHandle == nint.Zero)
            {
                LastError = $"安装鼠标钩子失败（错误码 {Marshal.GetLastWin32Error()}）。";
                _hookProc = null;
                return;
            }

            IsRunning = true;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"安装鼠标钩子时发生异常：{ex.Message}";
        }
    }

    /// <summary>卸载鼠标钩子。</summary>
    public static void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            if (_hookHandle != nint.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
            }
        }
        catch
        {
            // 忽略卸载异常
        }
        finally
        {
            _hookHandle = nint.Zero;
            _hookProc = null;
            IsRunning = false;
        }
    }

    private static nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        try
        {
            var message = (int)wParam;
            if (message is not (WmLeftButtonDown or WmRButtonDown))
            {
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            var point = new Point(data.Pt.X, data.Pt.Y);

            Entry[] snapshot;
            lock (Sync)
            {
                Entries.RemoveAll(x => !x.Anchor.TryGetTarget(out _));
                snapshot = Entries.ToArray();
            }

            foreach (var entry in snapshot)
            {
                if (!entry.Anchor.TryGetTarget(out var anchor) || !IsClickableNow(anchor))
                {
                    continue;
                }

                Rect? area;
                try
                {
                    area = entry.HitArea();
                }
                catch
                {
                    continue;
                }

                if (area is not { } rect || !rect.Contains(point))
                {
                    continue;
                }

                var isRightClick = message == WmRButtonDown;
                if (isRightClick && entry.OnRightClick == null)
                {
                    continue;
                }

                // 钩子回调必须在极短时间内返回：Windows 会对低级钩子设置超时
                // （默认约 300ms），回调超时会被系统静默卸载，导致后续点击全部失效。
                // 因此这里只做命中判定，真正的业务逻辑丢给 UI 线程异步执行。
                var handler = isRightClick ? entry.OnRightClick! : entry.OnClick;
                PostToUiThread(handler);

                // 吞掉这次点击：不再传递给下层窗口，也不传给 ClassIsland。
                return 1;
            }
        }
        catch
        {
            // 钩子回调里绝不能抛异常
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsClickableNow(Control anchor)
    {
        try
        {
            if (!anchor.IsEffectivelyVisible)
            {
                return false;
            }

            // 编辑模式下 ClassIsland 会把主界面变成可交互的普通窗口，此时交给它自己处理。
            return !MainWindowStylesAssist.GetMainWindowInEditMode(anchor);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 把实际的抽取逻辑投递到 UI 线程执行，避免在钩子回调里做业务处理而被系统判定超时。
    /// </summary>
    private static void PostToUiThread(Action handler)
    {
        try
        {
            Dispatcher.UIThread.Post(handler, DispatcherPriority.Input);
        }
        catch
        {
            // 不在 UI 线程或调度失败时忽略，绝不能影响钩子返回。
        }
    }
}
