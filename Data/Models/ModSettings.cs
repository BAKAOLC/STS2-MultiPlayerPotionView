using System.Text.Json.Serialization;

namespace STS2MultiPlayerPotionView.Data.Models
{
    public sealed class ModSettings
    {
        public const int CurrentDataVersion = 1;

        [JsonPropertyName("data_version")] public int DataVersion { get; set; } = CurrentDataVersion;

        [JsonPropertyName("content_scale")] public float ContentScale { get; set; } = 1.0f;

        [JsonPropertyName("highlight_keywords")]
        public List<HighlightKeywordEntry> HighlightKeywords { get; set; } = [];

        [JsonPropertyName("highlight_color_hex")]
        public string HighlightColorHex { get; set; } = "#FFD740FF";
    }
}
