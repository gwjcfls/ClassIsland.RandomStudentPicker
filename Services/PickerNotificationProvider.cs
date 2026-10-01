using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using Microsoft.Extensions.Logging;

// ClassIsland.Shared 与 ClassIsland.Core 下都有 NotificationRequest，直接引入整个命名空间会产生歧义，
// 这里只给需要的 NotificationSettings 起个别名。
using NotificationSettings = ClassIsland.Shared.Models.Notification.NotificationSettings;

namespace ClassIsland.RandomStudentPicker.Services;

/// <summary>
/// 抽取结果提醒提供方。每次抽到同学后会弹出一条 ClassIsland 提醒，
/// 并按设置决定是否朗读姓名。
/// </summary>
[NotificationProviderInfo(
    "6D1E4A72-8B3C-4F59-A1D6-9E0C7B52F483",
    "随机抽取同学",
    "\uECAA",
    "在随机抽取到同学时显示抽取结果。")]
public class PickerNotificationProvider : NotificationProviderBase
{
    private readonly RandomPickerService _picker;
    private readonly ILogger<PickerNotificationProvider> _logger;

    public PickerNotificationProvider(RandomPickerService picker, ILogger<PickerNotificationProvider> logger)
    {
        _picker = picker;
        _logger = logger;
        _picker.Picked += OnPicked;
    }

    private void OnPicked(object? sender, PickedEventArgs e)
    {
        try
        {
            var settings = _picker.Settings;
            var text = string.IsNullOrWhiteSpace(settings.NotifyPrefix)
                ? e.Name
                : settings.NotifyPrefix + e.Name;

            var duration = TimeSpan.FromSeconds(settings.NotifyDurationSeconds);
            var request = new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(text, factory: content =>
                {
                    content.Duration = duration;
                    content.IsSpeechEnabled = settings.EnableSpeech;
                }),

                // 关闭本次提醒的「提醒特效」。
                //
                // ClassIsland 在播放遮罩提醒时，若启用了提醒特效，会调用
                // TopmostEffectWindow.PlayEffect(...) 播放涟漪特效，而该特效是渲染在一个
                // 独立的置顶窗口（标题为「顶层效果窗口」）里的——抽取之后屏幕上就会多出这一层窗口。
                // 这里显式关掉特效，遮罩提醒本身照常在主界面上显示，只是不再弹那一层窗口。
                //
                // 注意 NotificationWorkerService 合并提醒设置时，是「取第一个
                // IsSettingsEnabled == true 的设置对象整体生效」，而不是逐字段叠加，
                // 所以这里要把其余几项按需显式写出来，避免被默认值（例如语音默认关闭）覆盖掉。
                RequestNotificationSettings = new NotificationSettings
                {
                    IsSettingsEnabled = true,
                    IsNotificationEnabled = true,
                    IsSpeechEnabled = true,
                    IsNotificationSoundEnabled = false,
                    IsNotificationTopmostEnabled = true,
                    IsNotificationEffectEnabled = false
                }
            };
            ShowNotification(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示随机抽取结果提醒时发生错误");
        }
    }
}
