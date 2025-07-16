using System.Text.Json.Serialization;

namespace MinimalChessEngine
{
    public class BookMove
    {
        [JsonPropertyName("uci")]
        public string Uci { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        [JsonPropertyName("is_gambit")]
        public bool IsGambit { get; set; }
    }
}