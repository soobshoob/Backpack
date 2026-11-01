using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 4I, п. 1: общая шапка страницы. «Назад» и «Сбросить раздел» центрированы по первой строке крупного названия, сброс —
/// у правого края области содержимого (отступ как у карточек) и не пересекает кнопки окна, перенос названия их не сдвигает.
/// </summary>
public class Phase4IHeaderTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var lang in new[] { UiLanguage.English, UiLanguage.Ukrainian, UiLanguage.German, UiLanguage.Greek })
        {
            foreach (double width in new[] { 1400.0, 760.0 })
            {
                foreach (bool longTitle in new[] { false, true })
                {
                    foreach (string? font in new[] { null, "Segoe UI" })
                    {
                        yield return [lang, width, longTitle, font ?? ""];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Back_and_reset_are_centred_on_the_first_title_line_and_reset_is_at_the_right_edge(UiLanguage lang, double width, bool longTitle, string font)
    {
        Sta.Run(() =>
        {
            Loc.Set(lang);
            var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Mouse)
            {
                Left = -32000, Top = -32000, Width = width, Height = 800, ShowActivated = false, ShowInTaskbar = false,
            };
            try
            {
                w.Show();
                var header = w.Header!;
                var title = (TextBlock)header.Children[1];
                if (longTitle)
                {
                    title.Text = "Mouse at the cursor, click circle and scroll arrows with a deliberately long page title that wraps";
                }

                if (font.Length > 0)
                {
                    title.FontFamily = new FontFamily(font);
                }

                w.UpdateLayout();
                var root = (System.Windows.Media.Visual)w.Content;
                var back = PageHeader.BoundsIn((UIElement)header.Children[0], root);
                var reset = PageHeader.BoundsIn((UIElement)header.Children[2], root);
                var t = PageHeader.BoundsIn(title, root);
                var h = PageHeader.BoundsIn(header, root);
                double line = header.FirstLineHeight;
                double firstLineCenter = t.Top + (line / 2);
                string at = $"{lang} {width} long={longTitle} font={font}: back {back} reset {reset} title {t} header {h}";

                Assert.True(Math.Abs((back.Top + (back.Height / 2)) - firstLineCenter) <= 1, at);
                Assert.True(Math.Abs((reset.Top + (reset.Height / 2)) - firstLineCenter) <= 1, at);
                Assert.True(Math.Abs(reset.Right - h.Right) <= 1, at);                                 // у правого края шапки
                Assert.True(Math.Abs(((FrameworkElement)root).ActualWidth - reset.Right - 24) <= 1.5, at);   // отступ как у карточек
                Assert.True(reset.Top >= WindowShell.CaptionButtonsBottom, at);                         // ниже кнопок окна
                Assert.True(t.Right <= reset.Left - PageHeader.Gap + 1, at);                           // название не наезжает на действие
                if (longTitle)
                {
                    Assert.True(t.Height > line * 1.5, at);                                              // перенос, а не обрезка
                }
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
                Loc.Set(UiLanguage.English);
            }
        });
    }
}
