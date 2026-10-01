using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.RandomStudentPicker.Models;

namespace ClassIsland.RandomStudentPicker.Components;

/// <summary>
/// 「随机抽取同学」组件的设置界面，显示在【应用设置】-【组件】的组件设置抽屉中。
/// </summary>
public partial class RandomPickerComponentSettingsControl : ComponentBase<PickComponentSettings>
{
    public RandomPickerComponentSettingsControl()
    {
        InitializeComponent();
    }
}
