using System.Text.Json.Serialization;

namespace STS2MultiPlayerPotionView.Data.Models
{
    public sealed class HighlightKeywordEntry
    {
        [JsonPropertyName("keyword")] public string Keyword { get; set; } = string.Empty;
    }
}
