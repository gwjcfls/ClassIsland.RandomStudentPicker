using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.RandomStudentPicker.Models;
using ClassIsland.RandomStudentPicker.Services;
using ClassIsland.Shared;

namespace ClassIsland.RandomStudentPicker.Views.SettingsPages;

/// <summary>
/// 插件设置页面，用于维护备选同学名单与抽取选项。
/// </summary>
[SettingsPageInfo(
    "classisland.random-student-picker.settings",
    "随机抽取同学",
    "\uECAB",
    "\uECAA")]
public partial class PickerSettingsPage : SettingsPageBase
{
    private readonly RandomPickerService _picker;

    /// <summary>
    /// 无参构造函数，供 Avalonia XAML 运行时加载器使用。
    /// </summary>
    public PickerSettingsPage() : this(null!)
    {
    }

    public PickerSettingsPage(RandomPickerService picker)
    {
        _picker = picker ?? IAppHost.TryGetService<RandomPickerService>()!;
        InitializeComponent();

        DataContext = this;

        RosterTextBox.Text = string.Join(Environment.NewLine, _picker.Settings.Students);
        SyncClickModeComboBox();
        SyncNotifyDurationSlider();
        UpdateSummary();
    }

    /// <summary>
    /// 把「提醒显示时长」滑块同步到当前设置，并在右侧显示具体秒数。
    /// </summary>
    private void SyncNotifyDurationSlider()
    {
        NotifyDurationSlider.Value = _picker.Settings.NotifyDurationSeconds;
        NotifyDurationTextBlock.Text = FormatSeconds(NotifyDurationSlider.Value);
    }

    private static string FormatSeconds(double value) => $"{(int)Math.Round(value)} 秒";

    private void NotifyDurationSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (NotifyDurationTextBlock == null)
        {
            return;
        }

        // 拖动过程中实时显示数值；松手后（步进对齐到整数）才写回设置。
        NotifyDurationTextBlock.Text = FormatSeconds(e.NewValue);

        var seconds = (int)Math.Round(e.NewValue);
        if (seconds != _picker.Settings.NotifyDurationSeconds)
        {
            _picker.Settings.NotifyDurationSeconds = seconds;
            _picker.Save();
        }
    }

    private void SyncClickModeComboBox()
    {
        var mode = _picker.Settings.ClickCaptureMode;
        foreach (var item in ClickModeComboBox.Items)
        {
            if (item is not ComboBoxItem comboBoxItem)
            {
                continue;
            }

            if (comboBoxItem.Tag is string tag && int.TryParse(tag, out var value) && value == mode)
            {
                ClickModeComboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        ClickModeComboBox.SelectedIndex = 0;
    }

    private void ClickMode_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ClickModeComboBox.SelectedItem is ComboBoxItem { Tag: string tag } &&
            int.TryParse(tag, out var mode))
        {
            _picker.Settings.ClickCaptureMode = mode;
            _picker.Save();
        }
    }

    /// <summary>插件设置（备选同学名单、抽取选项）。</summary>
    public PickerSettings Settings => _picker.Settings;

    private void UpdateSummary()
        => RosterSummaryTextBlock.Text = $"当前共 {_picker.StudentCount} 位同学";

    private void RosterTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        => UpdateSummary();

    private void ApplyRosterFromText()
    {
        var lines = (RosterTextBox.Text ?? "")
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);

        _picker.ReplaceRoster(lines);
        _picker.Save();
        UpdateSummary();
    }

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        ApplyRosterFromText();
        ShowStatus(true, $"已保存，共 {_picker.StudentCount} 位同学。");
    }

    private void AppendSample_OnClick(object? sender, RoutedEventArgs e)
    {
        var existing = (RosterTextBox.Text ?? "")
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        foreach (var name in PickerSettings.DefaultStudents)
        {
            if (!existing.Contains(name))
            {
                existing.Add(name);
            }
        }

        RosterTextBox.Text = string.Join(Environment.NewLine, existing);
        ApplyRosterFromText();
        ShowStatus(true, $"已追加示例名单，共 {_picker.StudentCount} 位同学。");
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e)
    {
        RosterTextBox.Text = "";
        ApplyRosterFromText();
        ShowStatus(true, "名单已清空。请重新填写后再使用抽取功能。");
    }

    private void PickOnce_OnClick(object? sender, RoutedEventArgs e)
    {
        ApplyRosterFromText();

        var name = _picker.Pick();
        if (name == null)
        {
            ShowStatus(false, _picker.Settings.EmptyRosterHint);
            return;
        }

        ShowStatus(true, $"抽到：{name}");
    }

    private void ShowStatus(bool success, string message)
    {
        StatusTextBlock.Text = (success ? "✓ " : "✗ ") + message;
        StatusTextBlock.IsVisible = true;
    }
}
