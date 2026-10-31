using System.Windows;
using System.Windows.Controls;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4H, п. 10: панель значков с меню «Ещё» — редкие действия уходят в меню только при нехватке ширины.</summary>
public class Phase4HToolbarTests
{
    [Fact]
    public void Rare_actions_move_to_more_menu_only_when_the_row_does_not_fit()
    {
        Sta.Run(() =>
        {
            var ui = new SettingsUi(UiTheme.Resources);
            int imported = 0;
            Button A(string icon, string text, System.Action? click = null, bool enabled = true) => ui.Action(icon, text, click ?? (() => { }), enabled, "why");
            var import = A(ActionIcons.Import, "Import", () => imported++);
            var bar = ui.Toolbar([[A(ActionIcons.Save, "Save"), A(ActionIcons.SaveAsNew, "Save as")], [A(ActionIcons.Duplicate, "Duplicate")]],
                [import, A(ActionIcons.Export, "Export", enabled: false)], [A(ActionIcons.Delete, "Delete")]);
            var host = new Border { Child = bar };
            var groups = bar.Children.Cast<FrameworkElement>().ToList();
            var rare = groups[2];
            var more = groups[3];

            void Layout(double width)
            {
                host.Width = width;
                host.Measure(new Size(width, 400));
                host.Arrange(new Rect(0, 0, width, 400));
                host.UpdateLayout();
            }

            Layout(900);
            Assert.Equal(Visibility.Visible, rare.Visibility);
            Assert.Equal(Visibility.Collapsed, more.Visibility);
            Layout(200);
            Assert.Equal(Visibility.Collapsed, rare.Visibility);
            Assert.Equal(Visibility.Visible, more.Visibility);
            Assert.Equal(Visibility.Visible, groups[^1].Visibility); // удаление остаётся на панели

            // Каждое действие — значок с доступным именем и подсказкой; недоступное объясняет причину.
            Assert.Equal("Import", System.Windows.Automation.AutomationProperties.GetName(import));
            Assert.Contains("why", (string)((Button)((Panel)rare).Children[2]).ToolTip);
            import.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, import));
            Assert.Equal(1, imported);
        });
    }
}
