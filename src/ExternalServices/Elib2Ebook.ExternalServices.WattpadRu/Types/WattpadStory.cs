using System.Text.Json.Serialization;

namespace Elib2Ebook.ExternalServices.WattpadRu.Types;

internal class WattpadStory
{
    [JsonPropertyName("authors")]
    public WattpadAuthor[] Authors { get; set; }

    [JsonPropertyName("cover")]
    public string Cover { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}
