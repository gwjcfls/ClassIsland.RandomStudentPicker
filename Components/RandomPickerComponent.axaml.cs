using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.RandomStudentPicker.Helpers;
using ClassIsland.RandomStudentPicker.Models;
using ClassIsland.RandomStudentPicker.Services;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;

namespace ClassIsland.RandomStudentPicker.Components;

/// <summary>
/// 主界面「随机抽取同学」组件。显示一个按钮，点击后从备选同学名单中随机抽出一位同学。
/// </summary>
[ComponentInfo(
    "7A4F3C61-2B8E-4D95-9F32-5C1E86B7A0D4",
    "随机抽取同学",
    "lucide(\ue2c5)",
    "显示一个「随机抽取」按钮，点击后从备选同学名单中随机抽出一位同学。")]
public partial class RandomPickerComponent : ComponentBase<PickComponentSettings>
{
    /// <summary>点击判定在按钮四周额外放宽的像素数，让按钮边缘也好点。</summary>
    private const double HitPadding = 3;

    /// <summary>检查鼠标是否停在按钮上的间隔（毫秒）。</summary>
    private const int FadeGuardIntervalMs = 120;

    private readonly RandomPickerService _picker;
    private readonly ILogger<RandomPickerComponent>? _logger;

    private readonly IslandFadeGuard _fadeGuard = new();

    private IDisposable? _clickRegistration;
    private DispatcherTimer? _feedbackTimer;
    private DispatcherTimer? _fadeGuardTimer;
    private DateTime _lastPickTimeUtc = DateTime.MinValue;
    private int _appliedMode = -1;

    /// <summary>无参构造函数，供 Avalonia XAML 运行时加载器使用。</summary>
    public RandomPickerComponent() : this(null, null)
    {
    }

    public RandomPickerComponent(RandomPickerService? picker, ILogger<RandomPickerComponent>? logger = null)
    {
        _picker = picker ?? IAppHost.TryGetService<RandomPickerService>()!;
        _logger = logger;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ApplyFontColor();
        RefreshResultText();
        ApplyClickMode();
        ApplyFadeGuard();

        _picker.Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _picker.Settings.PropertyChanged -= SettingsOnPropertyChanged;

        _clickRegistration?.Dispose();
        _clickRegistration = null;
        _feedbackTimer?.Stop();
        _feedbackTimer = null;
        _appliedMode = -1;
        _fadeGuardTimer?.Stop();
        _fadeGuardTimer = null;
        _fadeGuard.Release();

        base.OnDetachedFromVisualTree(e);
    }

    private void SettingsOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PickerSettings.ClickCaptureMode):
                ApplyClickMode();
                break;
            case nameof(PickerSettings.KeepIslandVisible):
                ApplyFadeGuard();
                break;
            case nameof(PickComponentSettings.UseCustomFontColor):
            case nameof(PickComponentSettings.FontColor):
                ApplyFontColor();
                break;
            case nameof(PickComponentSettings.ShowResultInComponent):
                RefreshResultText();
                break;
        }
    }

    #region 抽取期间保持主界面不淡化

    /// <summary>
    /// 按设置启停「保持主界面不淡化」的轮询。
    /// </summary>
    /// <remarks>
    /// 点击抽取按钮之后鼠标必然停留在主界面上，ClassIsland 会因此把整个主界面行淡化到
    /// 不透明度 0.05，连抽取结果提醒也一起看不清。这里在「鼠标停在抽取按钮上」或
    /// 「刚抽完、提醒还在显示」时临时压住淡化效果。
    /// </remarks>
    private void ApplyFadeGuard()
    {
        _fadeGuardTimer?.Stop();

        if (!_picker.Settings.KeepIslandVisible || !OperatingSystem.IsWindows())
        {
            _fadeGuard.Release();
            return;
        }

        _fadeGuardTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FadeGuardIntervalMs) };
        _fadeGuardTimer.Tick -= FadeGuardTimerOnTick;
        _fadeGuardTimer.Tick += FadeGuardTimerOnTick;
        _fadeGuardTimer.Start();
        UpdateFadeGuard();
    }

    private void FadeGuardTimerOnTick(object? sender, EventArgs e) => UpdateFadeGuard();

    private void UpdateFadeGuard()
    {
        var onButton = IsCursorOnPickButton();
        var inNotifyPeriod = IsWithinPickNotifyPeriod();

        if (_picker.Settings.KeepIslandVisible && (onButton || inNotifyPeriod))
        {
            _fadeGuard.Suppress(this);
        }
        else
        {
            _fadeGuard.Release();
        }

        LogFadeGuardState(onButton, inNotifyPeriod);
    }

    private string? _lastFadeGuardSignature;

    /// <summary>状态发生变化时记录一次日志，方便排查「什么时候没有压住淡化」。</summary>
    private void LogFadeGuardState(bool onButton, bool inNotifyPeriod)
    {
        var suppressed = _fadeGuard.IsActive;
        var signature = $"{onButton}|{inNotifyPeriod}|{suppressed}";
        if (signature == _lastFadeGuardSignature)
        {
            return;
        }

        _lastFadeGuardSignature = signature;
        _logger?.LogDebug(
            "淡化守卫: 鼠标在按钮上={OnButton} 在提醒时段内={InNotify} 已压住淡化={Suppressed} | 按钮区={Area} 光标={Cursor} | {Line}",
            onButton, inNotifyPeriod, suppressed,
            GetScreenHitArea() is { } r ? $"({r.Left:F0},{r.Top:F0})-({r.Right:F0},{r.Bottom:F0})" : "<null>",
            GetCursorPos(out var p) ? $"({p.X},{p.Y})" : "<失败>",
            _fadeGuard.DescribeLine(this));
    }

    /// <summary>鼠标当前是否停在抽取按钮的点击区域上。</summary>
    private bool IsCursorOnPickButton()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            if (!GetCursorPos(out var point))
            {
                return false;
            }

            return GetScreenHitArea()?.Contains(new Point(point.X, point.Y)) == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>当前是否处于「刚抽完、提醒正在显示」的时间窗内。</summary>
    private bool IsWithinPickNotifyPeriod()
    {
        if (_lastPickTimeUtc == DateTime.MinValue)
        {
            return false;
        }

        // 提醒显示期间保持不淡化，另外多留 1 秒余量，避免提醒刚消失界面就整行淡掉。
        var seconds = Math.Clamp(_picker.Settings.NotifyDurationSeconds, 2, 60) + 1;
        return DateTime.UtcNow - _lastPickTimeUtc < TimeSpan.FromSeconds(seconds);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    #endregion

    /// <summary>
    /// 根据插件设置切换「捕捉主界面点击」的方式。
    /// </summary>
    /// <remarks>
    /// ClassIsland 主界面在非编辑模式下是点击穿透的，组件里的控件也收不到指针事件（组件按设计只用于展示），
    /// 因此唯一的可行手段是从系统层面截获鼠标点击：模式 1 用低级鼠标钩子；模式 0 完全关闭，只做显示。
    /// </remarks>
    private void ApplyClickMode()
    {
        var mode = _picker.Settings.ClickCaptureMode;
        if (mode == _appliedMode)
        {
            return;
        }

        _clickRegistration?.Dispose();
        _clickRegistration = null;
        _appliedMode = mode;

        if (mode != 1)
        {
            return;
        }

        // 钩子回调内部会把真正的抽取投递到 UI 线程，这里传入的回调是安全的。
        _clickRegistration = IslandClickCatcher.Register(this, GetScreenHitArea, PickWithFeedback);
        _logger?.LogInformation(
            "已启用「全局鼠标钩子」点击捕捉（IsRunning={Running}, 错误={Error}）",
            IslandClickCatcher.IsRunning, IslandClickCatcher.LastError ?? "无");
    }

    private void ApplyFontColor()
    {
        if (Settings.UseCustomFontColor)
        {
            var brush = new SolidColorBrush(Settings.FontColor);
            ButtonTextBlock.Foreground = brush;
            ResultTextBlock.Foreground = brush;
        }
        else
        {
            ButtonTextBlock.ClearValue(TextBlock.ForegroundProperty);
            ResultTextBlock.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    /// <summary>
    /// 刷新组件上显示的抽取结果。
    /// </summary>
    /// <remarks>
    /// 还没有抽过、或者名单为空时都不显示任何文字，避免在 ClassIsland 主界面上出现
    /// 「点我抽一位同学」这类没有信息量的提示。
    /// </remarks>
    private void RefreshResultText()
    {
        if (!Settings.ShowResultInComponent)
        {
            ResultTextBlock.Text = "";
            ResultTextBlock.IsVisible = false;
            return;
        }

        var name = _picker.LastPicked;
        if (string.IsNullOrEmpty(name))
        {
            ResultTextBlock.Text = "";
            ResultTextBlock.IsVisible = false;
            return;
        }

        ResultTextBlock.Text = name;
        ResultTextBlock.IsVisible = true;
    }

    // 主界面在编辑模式下是普通可交互窗口，此时按钮的常规点击事件可以正常收到。
    private void PickButton_OnClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        PickWithFeedback();
    }

    /// <summary>抽取一次，并给按钮一点视觉反馈。</summary>
    private void PickWithFeedback()
    {
        // 钩子回调在钩子线程上触发，按钮的视觉反馈必须在 UI 线程上执行。
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(PickWithFeedback, DispatcherPriority.Input);
            return;
        }

        PlayClickFeedback();
        PickOnce();
    }

    private void PlayClickFeedback()
    {
        try
        {
            PickButton.Opacity = 0.55;
            _feedbackTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
            _feedbackTimer.Stop();
            _feedbackTimer.Tick -= FeedbackTimerOnTick;
            _feedbackTimer.Tick += FeedbackTimerOnTick;
            _feedbackTimer.Start();
        }
        catch
        {
            // 反馈动画失败无所谓
        }
    }

    private void FeedbackTimerOnTick(object? sender, EventArgs e)
    {
        _feedbackTimer?.Stop();
        try
        {
            PickButton.Opacity = 1.0;
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>执行一次抽取。</summary>
    public void PickOnce()
    {
        if (_picker.StudentCount == 0)
        {
            // 名单为空时不在主界面上写提示文字，只记录日志；提示文字只在设置页 / 提醒里出现。
            _logger?.LogWarning("备选同学名单为空，无法抽取。");
            return;
        }

        var name = _picker.Pick();
        if (name == null)
        {
            return;
        }

        RefreshResultText();

        // 记下抽取时间：提醒显示期间要保持主界面不淡化，否则提醒会被淡化得看不清。
        _lastPickTimeUtc = DateTime.UtcNow;
        UpdateFadeGuard();

        _logger?.LogInformation("随机抽取到同学：{Name}", name);
    }

    /// <summary>
    /// 计算「抽取按钮」在屏幕坐标系（物理像素）下要覆盖的矩形。
    /// </summary>
    /// <remarks>
    /// 做法：取组件自身的屏幕原点，再用「相邻单位点的投影差」算出每个逻辑像素对应多少屏幕像素，
    /// 最后叠加按钮相对组件的局部偏移与尺寸。这样得到的范围与按钮在屏幕上的可见范围一致，
    /// 且不受主界面多层缩放 / 平移（Scale、LayoutTransform、画布偏移）影响。
    /// 四周另留 <see cref="HitPadding"/> 像素容差，让按钮边缘也好点。
    /// </remarks>
    private Rect? GetScreenHitArea()
    {
        if (!IsEffectivelyVisible || PickButton.Bounds.Width <= 0 || PickButton.Bounds.Height <= 0)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return null;
        }

        try
        {
            // 直接取按钮自身两个角点的屏幕投影：这是「按钮在屏幕上的实际矩形」，
            // 与主界面的缩放 / 平移无关，天然覆盖按钮的全部可见范围。
            var topLeft = PickButton.PointToScreen(new Point(0, 0));
            var bottomRight = PickButton.PointToScreen(
                new Point(PickButton.Bounds.Width, PickButton.Bounds.Height));

            var left = Math.Min(topLeft.X, bottomRight.X) - HitPadding;
            var top = Math.Min(topLeft.Y, bottomRight.Y) - HitPadding;
            var right = Math.Max(topLeft.X, bottomRight.X) + HitPadding;
            var bottom = Math.Max(topLeft.Y, bottomRight.Y) + HitPadding;

            return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "计算随机抽取按钮点击区域失败");
            return null;
        }
    }
}
