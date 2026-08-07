using System.Globalization;
using DMTQ_Tools.Components.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace DMTQ.Tools.UITests;

[TestClass]
public sealed class LocalizationTests
{
    [TestMethod]
    public void RestoresSavedLanguageAndUpdatesCulture()
    {
        var store = new TestPreferenceStore { Value = "zh-CN" };

        var service = new LanguageService(store);

        service.CurrentCulture.Name.Should().Be("zh-CN");
        CultureInfo.CurrentUICulture.Name.Should().Be("zh-CN");
    }

    [TestMethod]
    public void ChangingLanguagePersistsAndNotifiesOnce()
    {
        var store = new TestPreferenceStore { Value = "en-US" };
        var service = new LanguageService(store);
        var notifications = 0;
        service.LanguageChanged += () => notifications++;

        service.SetLanguage("zh-CN");

        service.CurrentCulture.Name.Should().Be("zh-CN");
        store.Value.Should().Be("zh-CN");
        notifications.Should().Be(1);
    }

    [TestCleanup]
    public void RestoreTestCulture()
    {
        var culture = new CultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    [TestMethod]
    public void UsesSystemLanguageWithoutPersistingAutomaticChoice()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
            CultureInfo.CurrentUICulture = new CultureInfo("zh-TW");
            var store = new TestPreferenceStore();

            var service = new LanguageService(store);

            service.CurrentCulture.Name.Should().Be("zh-CN");
            store.Value.Should().BeNull();

            service.SetLanguage("zh-CN");

            store.Value.Should().Be("zh-CN");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
        }
    }

    [TestMethod]
    public void RejectsUnsupportedLanguage()
    {
        var service = new LanguageService(new TestPreferenceStore());

        var action = () => service.SetLanguage("ja-JP");

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void ResourceLocalizerUsesSelectedCulture()
    {
        var service = new LanguageService(new TestPreferenceStore());
        service.SetLanguage("zh-CN");
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .BuildServiceProvider();

        var localizer = provider.GetRequiredService<IStringLocalizer<AppStrings>>();

        localizer["Nav.Project"].Value.Should().Be("项目");
    }

    private sealed class TestPreferenceStore : ILanguagePreferenceStore
    {
        public string? Value { get; set; }
        public string? Get(string key) => Value;
        public void Set(string key, string value) => Value = value;
    }
}
