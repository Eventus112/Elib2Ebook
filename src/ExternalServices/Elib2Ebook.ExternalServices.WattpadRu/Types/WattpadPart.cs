using System.Text.Json.Serialization;

namespace Elib2Ebook.ExternalServices.WattpadRu.Types;

internal class WattpadPart
{
    [JsonPropertyName("chapter")]
    public string Chapter { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}
