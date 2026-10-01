using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using Microsoft.Extensions.Logging;

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
                })
            };
            ShowNotification(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示随机抽取结果提醒时发生错误");
        }
    }
}
