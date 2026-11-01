using System.Windows;

namespace Backpack.Visual.Settings;

/// <summary>Общие ресурсы и стили окон Backpack (Settings/SettingsTheme.xaml). Создаётся как обычный объект — без загрузки по URI.</summary>
public partial class SettingsTheme : ResourceDictionary
{
    public SettingsTheme() => InitializeComponent();

    public Style StyleOf(string key) => (Style)this[key];
}
