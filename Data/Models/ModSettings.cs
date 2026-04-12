using System.Text.Json.Serialization;

namespace STS2MultiPlayerPotionView.Data.Models
{
    public enum HighlightMatchMode
    {
        Text,
        Regex,
        Template,
    }

    public sealed class HighlightRuleEntry
    {
        [JsonPropertyName("pattern")] public string Pattern { get; set; } = string.Empty;
        [JsonPropertyName("color_hex")] public string ColorHex { get; set; } = string.Empty;
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("match_mode")] public HighlightMatchMode MatchMode { get; set; } = HighlightMatchMode.Text;
        [JsonPropertyName("rarities")] public List<string> Rarities { get; set; } = [];
        [JsonPropertyName("usages")] public List<string> Usages { get; set; } = [];
        [JsonPropertyName("target_types")] public List<string> TargetTypes { get; set; } = [];
        [JsonPropertyName("effect_terms")] public List<string> EffectTerms { get; set; } = [];
        [JsonPropertyName("require_usable")] public bool? RequireUsable { get; set; }
    }

    public sealed class ModSettings
    {
        public const int CurrentDataVersion = 2;
        public const double MinContentScale = 0.5d;
        public const double MaxContentScale = 5.0d;
        public const double MinPositionOffset = -200d;
        public const double MaxPositionOffset = 200d;

        [JsonPropertyName("data_version")] public int DataVersion { get; set; } = CurrentDataVersion;
        [JsonPropertyName("content_scale")] public double ContentScale { get; set; } = 1.0d;

        [JsonPropertyName("position_offset_x")]
        public double PositionOffsetX { get; set; }

        [JsonPropertyName("position_offset_y")]
        public double PositionOffsetY { get; set; }

        [JsonPropertyName("highlight_rules")] public List<HighlightRuleEntry> HighlightRules { get; set; } = [];

        [JsonPropertyName("highlight_keywords")]
        public List<HighlightKeywordEntry> HighlightKeywords { get; set; } = [];

        [JsonPropertyName("highlight_color_hex")]
        public string HighlightColorHex { get; set; } = "#FFD740FF";
    }
}
