using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.RandomStudentPicker.Components;
using ClassIsland.RandomStudentPicker.Helpers;
using ClassIsland.RandomStudentPicker.Models;
using ClassIsland.RandomStudentPicker.Services;
using ClassIsland.RandomStudentPicker.Views.SettingsPages;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.RandomStudentPicker;

/// <summary>
/// 「随机抽取同学」插件入口。
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>插件配置文件名。</summary>
    public const string SettingsFileName = "Settings.json";

    /// <summary>插件设置（备选同学名单等）。</summary>
    public PickerSettings Settings { get; private set; } = new();

    /// <inheritdoc />
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        EnsureAssemblyResolvable();

        var configPath = Path.Combine(PluginConfigFolder, SettingsFileName);
        Settings = ConfigureFileHelper.LoadConfig<PickerSettings>(configPath);

        var settings = Settings;
        services.AddSingleton(settings);
        services.AddSingleton(sp => new RandomPickerService(
            sp.GetRequiredService<PickerSettings>(),
            configPath,
            sp.GetService<ILogger<RandomPickerService>>(),
            sp.GetService<IComponentsService>()));

        // 抽取结果提醒
        services.AddNotificationProvider<PickerNotificationProvider>();

        // 主界面组件：随机抽取按钮
        services.AddComponent<RandomPickerComponent, RandomPickerComponentSettingsControl>();

        // 插件设置页面：备选同学名单
        services.AddSettingsPage<PickerSettingsPage>();

        AppBase.Current.AppStopping += (_, _) =>
        {
            try
            {
                // 退出前卸载鼠标钩子，并把名单落盘。
                Helpers.IslandClickCatcher.Stop();
                ConfigureFileHelper.SaveConfig(configPath, Settings, true);
            }
            catch
            {
                // 退出阶段忽略任何保存/卸载错误
            }
        };
    }

    /// <summary>
    /// 让 <c>avares://ClassIsland.RandomStudentPicker/...</c> 能被 Avalonia 解析到。
    /// </summary>
    /// <remarks>
    /// Avalonia 的资源加载器按名字用 <see cref="Assembly.Load(AssemblyName)"/> 查找程序集，走的是默认
    /// <see cref="AssemblyLoadContext"/>；而插件在独立的 PluginLoadContext 中，默认上下文看不到它，
    /// 不挂这个回调设置页面的 axaml 会加载失败。
    /// </remarks>
    private static void EnsureAssemblyResolvable()
    {
        var self = typeof(Plugin).Assembly;
        var selfName = self.GetName().Name;

        AssemblyLoadContext.Default.Resolving += (_, requested) =>
            requested.Name == selfName ? self : null;
    }
}
