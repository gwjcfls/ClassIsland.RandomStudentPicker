using System.Collections.ObjectModel;
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

    public RandomPickerService(PickerSettings settings, string configPath, ILogger<RandomPickerService>? logger = null)
    {
        Settings = settings;
        _configPath = configPath;
        _logger = logger;

        Settings.PropertyChanged += (_, _) => Save();
        Settings.Students.CollectionChanged += (_, _) => Save();
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
