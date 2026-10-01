using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClassIsland.RandomStudentPicker.Models;

/// <summary>
/// 插件级设置（备选同学名单 + 抽取选项），保存在插件配置目录的 Settings.json 中。
/// </summary>
public class PickerSettings : INotifyPropertyChanged
{
    private ObservableCollection<string> _students = new(DefaultStudents);
    private bool _avoidRepeat = true;
    private bool _enableSpeech = true;
    private string _notifyPrefix = "本次抽到：";
    private string _emptyRosterHint = "请先在插件设置中添加备选同学名单";
    private int _notifyDurationSeconds = 6;
    private int _clickCaptureMode = 1;
    private bool _keepIslandVisible = true;

    /// <summary>首次使用时的示例名单，方便用户直接看到效果。</summary>
    public static readonly string[] DefaultStudents =
    [
        "张三", "李四", "王五", "赵六", "钱七", "孙八"
    ];

    /// <summary>备选同学名单。</summary>
    public ObservableCollection<string> Students
    {
        get => _students;
        set
        {
            if (ReferenceEquals(value, _students)) return;
            _students = value ?? [];
            OnPropertyChanged();
            OnPropertyChanged(nameof(StudentCount));
        }
    }

    /// <summary>抽取时是否避免与上一次抽到的人重复。</summary>
    public bool AvoidRepeat
    {
        get => _avoidRepeat;
        set => SetField(ref _avoidRepeat, value);
    }

    /// <summary>抽出结果时是否通过 ClassIsland 提醒朗读姓名。</summary>
    public bool EnableSpeech
    {
        get => _enableSpeech;
        set => SetField(ref _enableSpeech, value);
    }

    /// <summary>提醒文本前缀。</summary>
    public string NotifyPrefix
    {
        get => _notifyPrefix;
        set => SetField(ref _notifyPrefix, value ?? "");
    }

    /// <summary>名单为空时组件上显示的提示文本。</summary>
    public string EmptyRosterHint
    {
        get => _emptyRosterHint;
        set => SetField(ref _emptyRosterHint, value ?? "");
    }

    /// <summary>提醒显示时长（秒）。</summary>
    public int NotifyDurationSeconds
    {
        get => _notifyDurationSeconds;
        set => SetField(ref _notifyDurationSeconds, Math.Clamp(value, 2, 60));
    }

    /// <summary>
    /// 主界面点击捕捉方式。
    /// <para>1 = 全局鼠标钩子（默认）：不动主界面窗口样式，直接捕捉落在组件上的真实点击，
    /// 并且会把这一次点击「吃掉」，不会同时点到主界面后面的窗口。</para>
    /// <para>0 = 关闭：组件只用于显示抽取结果，抽取请用设置页里的「试抽一次」。</para>
    /// </summary>
    /// <remarks>
    /// 旧版本存在过的「悬停时临时放开点击穿透」模式已移除：经实测，即使清掉主界面窗口的
    /// <c>WS_EX_TRANSPARENT</c>，ClassIsland 主界面上的组件内容依然收不到任何指针事件
    /// （组件按设计只用于展示），因此该模式不可能生效。读到 2 时自动回退到 1。
    /// </remarks>
    public int ClickCaptureMode
    {
        get => _clickCaptureMode;
        set => SetField(ref _clickCaptureMode, value == 0 ? 0 : 1);
    }

    /// <summary>名单人数（只读计算属性，不写入配置文件）。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int StudentCount => Students?.Count ?? 0;

    /// <summary>
    /// 鼠标停在抽取按钮上、以及抽取后提醒显示的这段时间里，是否让主界面保持不淡化。
    /// </summary>
    /// <remarks>
    /// ClassIsland 默认会在鼠标移入主界面时把整行淡化到几乎透明（不透明度 0.05），
    /// 而点击抽取按钮之后鼠标必定还在主界面上，会导致抽取结果提醒看不清。
    /// 开启本项后，插件会在这段时间里临时压住淡化效果，不改动 ClassIsland 的任何设置。
    /// </remarks>
    public bool KeepIslandVisible
    {
        get => _keepIslandVisible;
        set => SetField(ref _keepIslandVisible, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
