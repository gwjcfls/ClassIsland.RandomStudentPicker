using System.Collections.ObjectModel;
using System.ComponentModel;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.RandomStudentPicker.Models;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace ClassIsland.RandomStudentPicker.Services;

/// <summary>抽取结果事件参数。</summary>
public class PickedEventArgs(string name) : EventArgs
{
    /// <summary>抽到的同学姓名。</summary>
    public string Name { get; } = name;
}

/// <summary>
/// 随机抽取服务。负责维护备选同学名单的读写，以及执行抽取。
/// </summary>
public class RandomPickerService
{
    private readonly string _configPath;
    private readonly ILogger<RandomPickerService>? _logger;
    private readonly Random _random = new();
    private readonly object _sync = new();

    /// <summary>上一次抽到的同学姓名。</summary>
    public string LastPicked { get; private set; } = "";

    /// <summary>插件设置（备选同学名单等）。</summary>
    public PickerSettings Settings { get; }

    /// <summary>抽到同学时触发。</summary>
    public event EventHandler<PickedEventArgs>? Picked;

    /// <summary>上一次的抽取结果被清空时触发（例如切换了组件配置）。</summary>
    public event EventHandler? LastPickedCleared;

    public RandomPickerService(
        PickerSettings settings,
        string configPath,
        ILogger<RandomPickerService>? logger = null,
        IComponentsService? componentsService = null)
    {
        Settings = settings;
        _configPath = configPath;
        _logger = logger;

        Settings.PropertyChanged += (_, _) => Save();
        Settings.Students.CollectionChanged += (_, _) => Save();

        // 切换「组件配置」（应用设置 → 组件 → 组件配置）时清空上一次的抽取结果，
        // 否则新配置的主界面上会继续显示上一次抽到的同学。
        // ComponentsService 在 Settings.CurrentComponentConfig 变化时会替换 CurrentComponents。
        if (componentsService != null)
        {
            componentsService.PropertyChanged += ComponentsServiceOnPropertyChanged;
        }
    }

    private void ComponentsServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IComponentsService.CurrentComponents))
        {
            return;
        }

        _logger?.LogInformation("检测到组件配置切换，已清空上一次的抽取结果。");
        ClearLastPicked();
    }

    /// <summary>清空上一次抽到的同学，并通知界面刷新。</summary>
    public void ClearLastPicked()
    {
        if (string.IsNullOrEmpty(LastPicked))
        {
            return;
        }

        LastPicked = "";
        LastPickedCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>把当前的设置写回磁盘。</summary>
    public void Save()
    {
        try
        {
            ConfigureFileHelper.SaveConfig(_configPath, Settings, true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存随机抽取同学插件设置失败");
        }
    }

    /// <summary>名单中有效（去空白）的姓名。</summary>
    public IReadOnlyList<string> ValidStudents =>
        Settings.Students
            .Select(x => x?.Trim() ?? "")
            .Where(x => x.Length > 0)
            .ToList();

    /// <summary>名单人数。</summary>
    public int StudentCount => ValidStudents.Count;

    /// <summary>
    /// 随机抽取一位同学。名单为空时返回 null。
    /// </summary>
    /// <param name="raiseEvent">是否触发 <see cref="Picked"/> 事件（即弹出提醒）。</param>
    public string? Pick(bool raiseEvent = true)
    {
        List<string> candidates;
        lock (_sync)
        {
            candidates = ValidStudents.ToList();
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var pool = candidates;
        if (Settings.AvoidRepeat && candidates.Count > 1 && !string.IsNullOrEmpty(LastPicked))
        {
            var filtered = candidates.Where(x => x != LastPicked).ToList();
            if (filtered.Count > 0)
            {
                pool = filtered;
            }
        }

        var name = pool[_random.Next(pool.Count)];
        LastPicked = name;

        if (raiseEvent)
        {
            Picked?.Invoke(this, new PickedEventArgs(name));
        }

        return name;
    }

    /// <summary>
    /// 用一行一个姓名的文本整体替换名单。
    /// </summary>
    public void ReplaceRoster(IEnumerable<string> lines)
    {
        var names = lines
            .Select(x => x?.Trim() ?? "")
            .Where(x => x.Length > 0)
            .ToList();

        Settings.Students = new ObservableCollection<string>(names);
        if (!names.Contains(LastPicked))
        {
            LastPicked = "";
        }
    }
}
