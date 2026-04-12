using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2MultiPlayerPotionView.Data;
using STS2MultiPlayerPotionView.Data.Models;

namespace STS2MultiPlayerPotionView.Utils
{
    internal static class PotionDisplaySettings
    {
        private const float DefaultSlotSize = 24f;
        private const float BaseSeparation = 2f;

        public static Vector2 GetSlotSize()
        {
            var scale = Math.Clamp(GetSettings().ContentScale, ModSettings.MinContentScale,
                ModSettings.MaxContentScale);
            return Vector2.One * (DefaultSlotSize * (float)scale);
        }

        public static float GetSeparation()
        {
            var scale = Math.Clamp(GetSettings().ContentScale, ModSettings.MinContentScale,
                ModSettings.MaxContentScale);
            return Mathf.Max(BaseSeparation, Mathf.Round(BaseSeparation * Mathf.Sqrt((float)scale)));
        }

        public static Vector2 GetAutoOffset()
        {
            var slotSize = GetSlotSize();
            var y = -Mathf.Max(0f, (slotSize.Y - DefaultSlotSize) * 0.35f);
            return new(0f, y);
        }

        public static Vector2 GetUserOffset()
        {
            var settings = GetSettings();
            return new((float)settings.PositionOffsetX, (float)settings.PositionOffsetY);
        }

        public static float GetContainerHeight()
        {
            return GetSlotSize().Y + 8f;
        }

        public static float GetContentWidth(int count)
        {
            if (count <= 0)
                return 0f;
            var slotWidth = GetSlotSize().X;
            return count * slotWidth + (count - 1) * GetSeparation();
        }

        public static bool TryGetHighlightColor(PotionModel potion, out Color color)
        {
            foreach (var rule in GetRules())
            {
                var validation = ValidateRule(rule);
                if (!validation.IsValid)
                    continue;
                if (!MatchesRule(rule, potion)) continue;
                color = GetRuleColor(rule.ColorHex);
                return true;
            }

            color = default;
            return false;
        }

        public static RuleValidationResult ValidateRule(HighlightRuleEntry rule)
        {
            return rule.MatchMode switch
            {
                HighlightMatchMode.Regex => ValidateRegex(rule.Pattern),
                HighlightMatchMode.Template => ValidatePotionTemplate(rule),
                _ => string.IsNullOrWhiteSpace(rule.Pattern)
                    ? RuleValidationResult.Invalid("rule.validation.pattern_required")
                    : RuleValidationResult.Valid(),
            };
        }

        public static Color GetRuleColor(string? colorHex)
        {
            return TryParseHexColor(colorHex, out var parsed) ? parsed : GetDefaultHighlightColor();
        }

        public static Color GetDefaultHighlightColor()
        {
            return TryParseHexColor(GetSettings().HighlightColorHex, out var parsed)
                ? parsed
                : new(1f, 215f / 255f, 64f / 255f);
        }

        private static IEnumerable<HighlightRuleEntry> GetRules()
        {
            return GetSettings().HighlightRules.Where(rule => rule.Enabled);
        }

        private static bool MatchesRule(HighlightRuleEntry rule, PotionModel potion)
        {
            return rule.MatchMode switch
            {
                HighlightMatchMode.Regex => GetNormalizedCandidates(potion).Any(text =>
                    Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
                HighlightMatchMode.Template => MatchesTemplateRule(rule, potion),
                _ => GetNormalizedCandidates(potion).Any(text =>
                    text.Contains(NormalizeForMatch(rule.Pattern), StringComparison.OrdinalIgnoreCase)),
            };
        }

        private static bool MatchesTemplateRule(HighlightRuleEntry rule, PotionModel potion)
        {
            if (rule.Rarities.Count > 0 && !rule.Rarities.Any(rarity =>
                    string.Equals(rarity, potion.Rarity.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (rule.Usages.Count > 0 && !rule.Usages.Any(usage =>
                    string.Equals(usage, potion.Usage.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (rule.TargetTypes.Count > 0 && !rule.TargetTypes.Any(target =>
                    string.Equals(target, potion.TargetType.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (rule.RequireUsable.HasValue && potion.PassesCustomUsabilityCheck != rule.RequireUsable.Value)
                return false;
            if (rule.EffectTerms.Count <= 0) return true;
            var candidates = GetNormalizedCandidates(potion).ToArray();
            return rule.EffectTerms.All(term =>
                candidates.Any(text =>
                    text.Contains(NormalizeForMatch(term), StringComparison.OrdinalIgnoreCase)));
        }

        private static RuleValidationResult ValidateRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return RuleValidationResult.Invalid("rule.validation.pattern_required");
            try
            {
                _ = Regex.Match(string.Empty, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return RuleValidationResult.Valid();
            }
            catch (ArgumentException ex)
            {
                return RuleValidationResult.Invalid("rule.validation.regex_invalid", ex.Message);
            }
        }

        private static RuleValidationResult ValidatePotionTemplate(HighlightRuleEntry rule)
        {
            var hasCondition = rule.Rarities.Count > 0 || rule.Usages.Count > 0 || rule.TargetTypes.Count > 0 ||
                               rule.EffectTerms.Count > 0 || rule.RequireUsable.HasValue;
            return hasCondition
                ? RuleValidationResult.Valid()
                : RuleValidationResult.Invalid("rule.validation.template_required");
        }

        private static IEnumerable<string> GetNormalizedCandidates(PotionModel potion)
        {
            return potion.HoverTips
                .SelectMany(GetTexts)
                .Select(NormalizeForMatch)
                .Where(text => !string.IsNullOrWhiteSpace(text));
        }

        private static string NormalizeForMatch(string text)
        {
            var withoutBbCode = text.StripBbCode();
            var withoutHtml = NSearchBar.RemoveHtmlTags(withoutBbCode);
            return NSearchBar.Normalize(withoutHtml);
        }

        private static IEnumerable<string> GetTexts(IHoverTip hoverTip)
        {
            if (hoverTip is not HoverTip concrete) yield break;
            if (!string.IsNullOrWhiteSpace(concrete.Title))
                yield return concrete.Title;
            if (!string.IsNullOrWhiteSpace(concrete.Description))
                yield return concrete.Description;
        }

        private static ModSettings GetSettings()
        {
            return ModDataStore.Get<ModSettings>(ModDataStore.SettingsKey);
        }

        private static bool TryParseHexColor(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (!trimmed.StartsWith('#'))
                trimmed = $"#{trimmed}";

            var hex = trimmed[1..];
            if (hex.Length is not (3 or 4 or 6 or 8) || hex.Any(c => !Uri.IsHexDigit(c)))
                return false;
            if (hex.Length is 3 or 4)
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            if (hex.Length == 6)
                hex += "FF";

            color = new(
                Convert.ToByte(hex[..2], 16) / 255f,
                Convert.ToByte(hex[2..4], 16) / 255f,
                Convert.ToByte(hex[4..6], 16) / 255f,
                Convert.ToByte(hex[6..8], 16) / 255f);
            return true;
        }
    }

    internal readonly record struct RuleValidationResult(bool IsValid, string LocalizationKey, string? Detail)
    {
        public static RuleValidationResult Valid()
        {
            return new(true, string.Empty, null);
        }

        public static RuleValidationResult Invalid(string localizationKey, string? detail = null)
        {
            return new(false, localizationKey, detail);
        }
    }
}
