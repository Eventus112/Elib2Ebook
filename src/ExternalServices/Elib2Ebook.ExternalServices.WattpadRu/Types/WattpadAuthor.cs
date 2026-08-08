using System.Text.Json.Serialization;

namespace Elib2Ebook.ExternalServices.WattpadRu.Types;

internal class WattpadAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}
