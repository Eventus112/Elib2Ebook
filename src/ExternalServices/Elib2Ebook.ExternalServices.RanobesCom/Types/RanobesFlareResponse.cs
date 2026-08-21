using System.Text.Json.Serialization;

namespace Elib2Ebook.ExternalServices.RanobesCom.Types;

internal sealed class RanobesFlareResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("solution")]
    public RanobesFlareSolution Solution { get; set; }
}

internal sealed class RanobesFlareSolution
{
    [JsonPropertyName("response")]
    public string Response { get; set; }

    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; }

    [JsonPropertyName("cookies")]
    public List<RanobesFlareCookie> Cookies { get; set; } = [];
}

internal sealed class RanobesFlareCookie
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}
