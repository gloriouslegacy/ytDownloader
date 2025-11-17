using System.Configuration;
using System.Data;
using System.Windows;
using ytDownloader.Services;

namespace ytDownloader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 설정 로드 및 테마/언어 적용
            var settingsService = new SettingsService();
            var settings = settingsService.LoadSettings();

            // 저장된 테마 적용
            ApplyTheme(settings.Theme);

            // 저장된 언어 적용
            ApplyLanguage(settings.Language);
        }

        private void ApplyTheme(string theme)
        {
            try
            {
                var dictionaries = this.Resources.MergedDictionaries;

                // 기존 테마 제거
                var existingTheme = dictionaries.FirstOrDefault(d =>
                    d.Source != null && (d.Source.OriginalString.Contains("LightTheme.xaml") || d.Source.OriginalString.Contains("DarkTheme.xaml")));
                if (existingTheme != null)
                {
                    dictionaries.Remove(existingTheme);
                }

                string themeFile = theme == "Light" ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
                var themeDict = new ResourceDictionary
                {
                    Source = new Uri(themeFile, UriKind.Relative)
                };

                dictionaries.Insert(0, themeDict);
            }
            catch
            {
                // 테마 적용 실패시 기본 다크 테마 사용
            }
        }

        private void ApplyLanguage(string language)
        {
            try
            {
                var dictionaries = this.Resources.MergedDictionaries;

                // 기존 언어 리소스 제거
                var existingLanguage = dictionaries.FirstOrDefault(d =>
                    d.Source != null && (d.Source.OriginalString.Contains("Korean.xaml") || d.Source.OriginalString.Contains("English.xaml")));
                if (existingLanguage != null)
                {
                    dictionaries.Remove(existingLanguage);
                }

                string languageFile = language == "en" ? "Resources/English.xaml" : "Resources/Korean.xaml";
                var languageDict = new ResourceDictionary
                {
                    Source = new Uri(languageFile, UriKind.Relative)
                };

                dictionaries.Add(languageDict);
            }
            catch
            {
                // 언어 적용 실패시 기본 한국어 사용
            }
        }
    }
}
