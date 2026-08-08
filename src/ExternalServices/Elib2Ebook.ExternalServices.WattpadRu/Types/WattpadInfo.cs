using System.Text.Json.Serialization;

namespace Elib2Ebook.ExternalServices.WattpadRu.Types;

internal class WattpadInfo
{
    [JsonPropertyName("story")]
    public WattpadStory Story { get; set; }

    [JsonPropertyName("parts")]
    public WattpadPart[] Parts { get; set; }
}
