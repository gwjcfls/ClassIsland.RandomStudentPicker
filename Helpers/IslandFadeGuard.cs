using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ClassIsland.Core.Models.Components;

namespace ClassIsland.RandomStudentPicker.Helpers;

/// <summary>
/// 在用户操作抽取按钮（以及抽取结果提醒显示期间）临时阻止 ClassIsland 把主界面行「淡化」。
/// </summary>
/// <remarks>
/// <para>
/// ClassIsland 的 <c>MainWindowLine</c> 在鼠标移入主界面区域时会把 <c>IsLineFaded</c> 置为 <c>true</c>，
/// 对应样式会把整行的 <see cref="Visual.OpacityProperty"/> 压到 <c>0.05</c>：
/// <code>
/// &lt;Style Selector="^[IsLineFaded=True][(ci|MainWindowStylesAssist.MainWindowInEditMode)=False]"&gt;
///     &lt;Setter Property="Opacity" Value="0.05"/&gt;
/// &lt;/Style&gt;
/// </code>
/// 于是抽取结果提醒也会一并变得几乎不可见——而点击抽取按钮之后鼠标必然停留在主界面上，
/// 正好命中这个淡化条件。
/// </para>
/// <para>
/// 这里给承载本组件的主界面行设置一个 <b>本地值</b> 的 Opacity（取该行自身的
/// <see cref="MainWindowLineSettings.Opacity"/>）。Avalonia 中本地值的优先级高于样式 setter，
/// 因此可以压过淡化样式而又不改动 ClassIsland 的任何设置或窗口样式；不需要时清除本地值，
/// 淡化行为立即完全恢复原样。
/// </para>
/// <para>
/// 插件只能引用 <c>ClassIsland.Core</c>，拿不到主程序集里的 <c>MainWindowLine</c> 类型，
/// 所以主界面行是通过「视觉树祖先 + Settings 属性的返回类型是 <see cref="MainWindowLineSettings"/>」
/// 来识别的；识别不到时静默放弃，不影响抽取功能本身。
/// </para>
/// </remarks>
public sealed class IslandFadeGuard
{
    private static readonly Dictionary<Type, PropertyInfo?> SettingsPropertyByType = [];

    private Control? _line;

    /// <summary>当前是否已经压住了淡化效果。</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// 让承载 <paramref name="anchor"/> 的主界面行保持正常不透明度。
    /// </summary>
    /// <param name="anchor">组件中的任意控件，用于沿视觉树向上查找主界面行。</param>
    public void Suppress(Control anchor)
    {
        var line = ResolveLine(anchor);
        if (line == null)
        {
            return;
        }

        var opacity = GetLineOpacity(line);
        if (opacity == null)
        {
            return;
        }

        try
        {
            _line = line;
            line.SetValue(Visual.OpacityProperty, opacity.Value);
            IsActive = true;
        }
        catch
        {
            // 设置失败时不影响抽取流程
        }
    }

    /// <summary>诊断用：描述当前识别到的主界面行及其淡化相关状态。</summary>
    /// <param name="anchor">组件中的任意控件。</param>
    internal string DescribeLine(Control anchor)
    {
        try
        {
            var line = ResolveLine(anchor);
            if (line == null)
            {
                return "line=<未找到>";
            }

            var type = line.GetType();
            object? Read(string name) => type.GetProperty(name)?.GetValue(line);

            return $"line={type.FullName} IsMouseIn={Read("IsMouseIn")} IsLineFaded={Read("IsLineFaded")} Opacity={line.Opacity}";
        }
        catch (Exception ex)
        {
            return $"line=<异常 {ex.GetType().Name}>";
        }
    }

    /// <summary>清除本地不透明度，恢复 ClassIsland 原本的淡化行为。</summary>
    public void Release()
    {
        var line = _line;
        _line = null;
        IsActive = false;

        if (line == null)
        {
            return;
        }

        try
        {
            line.ClearValue(Visual.OpacityProperty);
        }
        catch
        {
            // 主界面行可能已经被重建/销毁，忽略
        }
    }

    private Control? ResolveLine(Control anchor)
    {
        // 已经找到过且那一行还在可视树上，就直接复用，避免每次沿视觉树遍历。
        if (_line is { } cached && cached.GetVisualRoot() != null)
        {
            return cached;
        }

        if (anchor.GetVisualRoot() == null)
        {
            return null;
        }

        foreach (var ancestor in anchor.GetVisualAncestors())
        {
            if (ancestor is Control control && GetSettingsProperty(control.GetType()) != null)
            {
                return control;
            }
        }

        return null;
    }

    private static double? GetLineOpacity(Control line)
    {
        try
        {
            var property = GetSettingsProperty(line.GetType());
            if (property?.GetValue(line) is MainWindowLineSettings settings)
            {
                return settings.Opacity;
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    /// <summary>
    /// 取「返回 <see cref="MainWindowLineSettings"/> 的 Settings 属性」，命中即认为该控件是主界面行。
    /// </summary>
    private static PropertyInfo? GetSettingsProperty(Type type)
    {
        lock (SettingsPropertyByType)
        {
            if (SettingsPropertyByType.TryGetValue(type, out var cached))
            {
                return cached;
            }

            PropertyInfo? property = null;
            try
            {
                property = type.GetProperty("Settings", typeof(MainWindowLineSettings));
            }
            catch
            {
                // 忽略
            }

            SettingsPropertyByType[type] = property;
            return property;
        }
    }
}
