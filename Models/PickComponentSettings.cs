using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace ClassIsland.RandomStudentPicker.Models;

/// <summary>
/// 主界面「随机抽取同学」组件的设置。每个摆放在主界面上的组件实例各自独立。
/// </summary>
public class PickComponentSettings : INotifyPropertyChanged
{
    private string _buttonText = "随机抽取";
    private double _fontSize = 16;
    private bool _showIcon = true;
    private bool _showResultInComponent = true;
    private Color _fontColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private bool _useCustomFontColor = false;
    private double _spacing = 4;

    /// <summary>按钮上显示的文字。</summary>
    public string ButtonText
    {
        get => _buttonText;
        set => SetField(ref _buttonText, value ?? "");
    }

    /// <summary>组件文字大小。</summary>
    public double FontSize
    {
        get => _fontSize;
        set => SetField(ref _fontSize, Math.Clamp(value, 10, 40));
    }

    /// <summary>是否在按钮上显示图标。</summary>
    public bool ShowIcon
    {
        get => _showIcon;
        set => SetField(ref _showIcon, value);
    }

    /// <summary>是否在组件上显示上一次的抽取结果。关闭后只弹提醒。</summary>
    public bool ShowResultInComponent
    {
        get => _showResultInComponent;
        set => SetField(ref _showResultInComponent, value);
    }

    /// <summary>自定义文字颜色。</summary>
    public Color FontColor
    {
        get => _fontColor;
        set => SetField(ref _fontColor, value);
    }

    /// <summary>是否启用自定义文字颜色。</summary>
    public bool UseCustomFontColor
    {
        get => _useCustomFontColor;
        set => SetField(ref _useCustomFontColor, value);
    }

    /// <summary>按钮与结果之间的间距（像素）。</summary>
    public double Spacing
    {
        get => _spacing;
        set => SetField(ref _spacing, Math.Clamp(value, 0, 24));
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
