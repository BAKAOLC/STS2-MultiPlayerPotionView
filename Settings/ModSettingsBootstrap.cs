using Godot;
using STS2MultiPlayerPotionView.Data;
using STS2MultiPlayerPotionView.Data.Models;
using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace STS2MultiPlayerPotionView.Settings
{
    internal static class ModSettingsBootstrap
    {
        private static readonly Lock InitLock = new();
        private static bool _initialized;

        internal static void Initialize()
        {
            lock (InitLock)
            {
                if (_initialized)
                    return;

                var contentScaleBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, float>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.ContentScale,
                        (settings, value) => settings.ContentScale = value),
                    () => 1.0f);
                var colorBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, string>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.HighlightColorHex,
                        (settings, value) => settings.HighlightColorHex = value),
                    () => "#FFD740FF");
                var keywordListBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, List<HighlightKeywordEntry>>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.HighlightKeywords,
                        (settings, value) => settings.HighlightKeywords = value),
                    () => []);

                RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
                    .WithModDisplayName(ModSettingsLocalization.T("mod.displayName", "Multiplayer Potion View"))
                    .WithTitle(ModSettingsLocalization.T("page.title", "Settings"))
                    .WithDescription(ModSettingsLocalization.T("page.description",
                        "Adjust potion preview scale and highlight potions that match configured keywords."))
                    .AddSection("display", section => section
                        .WithTitle(ModSettingsLocalization.T("section.display", "Display"))
                        .AddSlider(
                            "content_scale",
                            ModSettingsLocalization.T("contentScale.label", "Content Size"),
                            contentScaleBinding,
                            0.5f,
                            5.0f,
                            0.05f,
                            value => $"{value:0.00}x",
                            ModSettingsLocalization.T("contentScale.description",
                                "Scales the potion icons shown beside each teammate."))
                        .AddList(
                            "highlight_keywords",
                            ModSettingsLocalization.T("keywords.label", "Highlight Keywords"),
                            keywordListBinding,
                            () => new HighlightKeywordEntry(),
                            item => ModSettingsText.Literal(string.IsNullOrWhiteSpace(item.Keyword)
                                ? ModSettingsLocalization.Get("keywords.emptyItem", "(empty keyword)")
                                : item.Keyword),
                            item => ModSettingsText.Literal(
                                string.IsNullOrWhiteSpace(item.Keyword)
                                    ? ModSettingsLocalization.Get("keywords.emptyDescription",
                                        "Items that contain this keyword, hover tip title, or description will receive a border.")
                                    : $"Matches potion content containing '{item.Keyword}'."),
                            CreateKeywordEditor,
                            ModSettingsStructuredData.Json<HighlightKeywordEntry>(),
                            ModSettingsLocalization.T("keywords.add", "Add Keyword"),
                            ModSettingsLocalization.T("keywords.description",
                                "Keywords are matched case-insensitively against potion tooltips."))
                        .AddColor(
                            "highlight_color",
                            ModSettingsLocalization.T("color.label", "Border Color"),
                            colorBinding,
                            ModSettingsLocalization.T("color.description",
                                "Includes a live preview, RGBA controls, and hex input such as #FFD740FF."))));

                _initialized = true;
            }
        }

        private static Control CreateKeywordEditor(ModSettingsListItemContext<HighlightKeywordEntry> itemContext)
        {
            var edit = new LineEdit
            {
                Text = itemContext.Item.Keyword,
                SelectAllOnFocus = true,
                PlaceholderText = ModSettingsLocalization.Get("keywords.placeholder", "e.g. Exhaust / Block / Poison"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(260f, 44f),
            };
            edit.AddThemeFontSizeOverride("font_size", 18);
            edit.AddThemeColorOverride("font_color", new Color(1f, 0.964706f, 0.886275f));
            edit.AddThemeStyleboxOverride("normal", CreateInputStyle(false));
            edit.AddThemeStyleboxOverride("focus", CreateInputStyle(true));
            edit.TextSubmitted += value =>
            {
                itemContext.Update(new HighlightKeywordEntry { Keyword = value.Trim() });
                edit.ReleaseFocus();
            };
            edit.FocusExited += () => itemContext.Update(new HighlightKeywordEntry { Keyword = edit.Text.Trim() });
            return edit;
        }

        private static StyleBoxFlat CreateInputStyle(bool focused)
        {
            return new StyleBoxFlat
            {
                BgColor = focused ? new Color(0.10f, 0.14f, 0.19f, 0.98f) : new Color(0.08f, 0.11f, 0.15f, 0.96f),
                BorderColor = focused ? new Color(0.92f, 0.74f, 0.32f, 0.9f) : new Color(0.36f, 0.49f, 0.60f, 0.5f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 14,
                ContentMarginTop = 10,
                ContentMarginRight = 14,
                ContentMarginBottom = 10,
            };
        }
    }
}
