using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Elib2Ebook.Domain.Book;
using Elib2Ebook.Domain.Common;
using Elib2Ebook.DomainServices.Configs;
using Elib2Ebook.DomainServices.Extensions;
using Elib2Ebook.DomainServices.Getters;
using Elib2Ebook.ExternalServices.RanobesCom.Types;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.Logging;

namespace Elib2Ebook.ExternalServices.RanobesCom;

public class RanobesComGetter(BookGetterConfig config) : GetterBase(config)
{
    protected override Uri SystemUrl => new("https://ranobes.com/");

    protected override string GetId(Uri url)
        => base.GetId(url).Split(".")[0];

    public override async Task<Book> Get(Uri url)
    {
        PrepareClient();
        url = await GetMainUrl(url);
        url = SystemUrl.MakeRelativeUri($"/ranobe/{GetId(url)}.html");
        var doc = await GetDocument(url);
        if (HasLegacyAntibot(doc))
        {
            await Antibot(doc, url);
            doc = await GetDocument(url);
        }

        var book = new Book(url)
        {
            Cover = await GetCover(doc, url),
            Chapters = await FillChapters(doc, url),
            Title = doc.QuerySelector("h1.title").FirstChild.InnerText.Trim().HtmlDecode(),
            Author = new Author(doc.GetTextBySelector(".r-fullstory-spec .tag_list a") ?? "Ranobes"),
            Annotation = doc.QuerySelector(".r-desription .cont-text")?.RemoveNodes("style")?.InnerHtml
        };

        return book;
    }

    private void PrepareClient()
    {
        if (string.IsNullOrWhiteSpace(Config.Options.Flare) && !Config.Client.DefaultRequestHeaders.UserAgent.Any())
        {
            Config.Client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
        }

        if (!Config.Client.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            Config.Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");
        }

        Config.CookieContainer.Add(SystemUrl, new Cookie("browser_check", "1"));
    }

    private static bool HasLegacyAntibot(HtmlDocument doc)
        => doc.ParsedText.Contains("antibot8/ab.php") && Regex.IsMatch(doc.ParsedText, "antibot_.*?=");

    private static bool IsBlocked(HtmlDocument doc)
        => doc.QuerySelector(".cf-turnstile") != null ||
           doc.QuerySelector("title")?.InnerText.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) == true;

    private static void EnsureNotBlocked(HtmlDocument doc, Uri uri)
    {
        if (IsBlocked(doc))
        {
            throw new InvalidOperationException(
                $"Ranobes вернул антибот вместо страницы {uri}. Повторите позже с --delay 1 или запустите с --flare http://flaresolverr:8191.");
        }
    }

    private async Task<HtmlDocument> GetDocument(Uri uri)
    {
        var doc = await Config.Client.GetHtmlDocWithTriesAsync(uri);
        if (IsBlocked(doc) && !string.IsNullOrWhiteSpace(Config.Options.Flare))
        {
            doc = await GetDocumentWithFlare(uri);
        }

        EnsureNotBlocked(doc, uri);
        return doc;
    }

    private async Task<HtmlDocument> GetDocumentWithFlare(Uri uri)
    {
        Config.Logger.LogInformation($"Ranobes запросил проверку браузера для {uri}. Использую FlareSolverr");

        using var flareClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Config.Options.Timeout)
        };
        var flareUri = new Uri(Config.Options.Flare.TrimEnd('/') + "/v1");
        using var response = await flareClient.PostAsJsonAsync(
            flareUri,
            new
            {
                cmd = "request.get",
                url = uri.ToString(),
                maxTimeout = Config.Options.Timeout * 1000
            });
        response.EnsureSuccessStatusCode();

        var flare = await response.Content.ReadFromJsonAsync<RanobesFlareResponse>();
        if (flare?.Status != "ok" || flare.Solution == null || string.IsNullOrWhiteSpace(flare.Solution.Response))
        {
            throw new InvalidOperationException($"FlareSolverr не смог открыть {uri}: {flare?.Message ?? "пустой ответ"}");
        }

        if (!string.IsNullOrWhiteSpace(flare.Solution.UserAgent))
        {
            Config.Client.DefaultRequestHeaders.UserAgent.Clear();
            Config.Client.DefaultRequestHeaders.UserAgent.ParseAdd(flare.Solution.UserAgent);
        }

        foreach (var cookie in flare.Solution.Cookies.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
        {
            Config.CookieContainer.Add(SystemUrl, new Cookie(cookie.Name, cookie.Value ?? string.Empty));
        }

        return flare.Solution.Response.AsHtmlDoc();
    }

    private async Task Antibot(HtmlDocument doc, Uri referrer)
    {
        var h1 = Regex.Match(doc.ParsedText, "var h1 = \'(?<id>.*?)\'").Groups["id"].Value;
        var h2 = Regex.Match(doc.ParsedText, "var h2 = \'(?<id>.*?)\'").Groups["id"].Value;
        var date = Regex.Match(doc.ParsedText, "var date = \'(?<id>.*?)\'").Groups["id"].Value;
        var cid = Regex.Match(doc.ParsedText, "var cid = \'(?<id>.*?)\'").Groups["id"].Value;
        var ip = Regex.Match(doc.ParsedText, "var ip = \'(?<id>.*?)\'").Groups["id"].Value;
        var ptr = Regex.Match(doc.ParsedText, "var ptr = \'(?<id>.*?)\'").Groups["id"].Value;
        var v = Regex.Match(doc.ParsedText, "var v = \'(?<id>.*?)\'").Groups["id"].Value;
        var antibot = Regex.Match(doc.ParsedText, "antibot_(?<id>.*?)=").Groups["id"].Value;

        var data = new Dictionary<string, string>
        {
            {
                "hdc", "0"
            },
            {
                "scheme", "https"
            },
            {
                "a", "0"
            },
            {
                "date", date
            },
            {
                "country", "RU"
            },
            {
                "h1", h1
            },
            {
                "h2", h2
            },
            {
                "ip", ip
            },
            {
                "v", v
            },
            {
                "cid", cid
            },
            {
                "ptr", ptr
            },
            {
                "w", "2560"
            },
            {
                "h", "1440"
            },
            {
                "cw", "2560"
            },
            {
                "ch", "770"
            },
            {
                "co", "24"
            },
            {
                "pi", "24"
            },
            {
                "ref", "ranobes.com"
            },
            {
                "xxx", string.Empty
            },
        };

        Config.Client.DefaultRequestHeaders.Add("Referer", referrer.ToString());
        var post = await Config.Client.PostAsync(SystemUrl.MakeRelativeUri("antibot8/ab.php"), new FormUrlEncodedContent(data));
        var cookie = await post.Content.ReadFromJsonAsync<RanobesCookie>();
        Config.CookieContainer.Add(SystemUrl, new Cookie($"antibot_{antibot}", cookie.Cookie + "-" + date));
        Config.Client.DefaultRequestHeaders.Remove("Referer");
    }

    private async Task<Uri> GetMainUrl(Uri url)
    {
        if (url.GetSegment(1) == "chapters" || !url.Segments.Last().EndsWith(".html"))
        {
            var doc = await GetDocument(SystemUrl.MakeRelativeUri(url.AbsolutePath));
            return url.MakeRelativeUri(doc.QuerySelector("a[rel=up]").Attributes["href"].Value);
        }

        return url;
    }

    private async Task<IEnumerable<Chapter>> FillChapters(HtmlDocument doc, Uri url)
    {
        var result = new List<Chapter>();
        if (Config.Options.NoChapters)
        {
            return result;
        }

        foreach (var ranobeChapter in await GetToc(GetTocLink(doc, url)))
        {
            Config.Logger.LogInformation($"Загружаю главу {ranobeChapter.Title.CoverQuotes()}");
            var chapter = new Chapter();
            var chapterDoc = await GetChapter(ranobeChapter);
            chapter.Images = await GetImages(chapterDoc, url);
            chapter.Content = chapterDoc.DocumentNode.InnerHtml;
            chapter.Title = ranobeChapter.Title;

            result.Add(chapter);
        }

        return result;
    }

    private async Task<HtmlDocument> GetChapter(UrlChapter chapter)
    {
        var doc = await GetDocument(chapter.Url);
        return ExtractChapter(doc);
    }

    internal static HtmlDocument ExtractChapter(HtmlDocument doc)
    {
        var article = doc.QuerySelector("#arrticle") ??
                      throw new InvalidOperationException("На странице Ranobes не найден текст главы (#arrticle)");

        // Chapter text is stored directly in text nodes separated by <br> tags.
        // QuerySelectorAll only returns element nodes, which used to discard the
        // actual prose and leave an empty chapter.
        return article.InnerHtml.AsHtmlDoc().RemoveNodes(
            node => node.Name == "script" ||
                    node.HasClass("splitnewsnavigation") ||
                    node.Id?.Contains("yandex_rtb") == true ||
                    node.Name == "div" && node.InnerHtml?.Contains("window.yaContextCb") == true);
    }

    private Task<TempFile> GetCover(HtmlDocument doc, Uri bookUri)
    {
        var imagePath = doc.QuerySelector("div.poster img")?.Attributes["src"]?.Value;
        return !string.IsNullOrWhiteSpace(imagePath) ? SaveImage(bookUri.MakeRelativeUri(imagePath)) : Task.FromResult(default(TempFile));
    }

    internal static Uri GetTocLink(HtmlDocument doc, Uri uri)
    {
        var relativeUri = doc.QuerySelector("div.r-fullstory-btns a[title~=оглавление]").Attributes["href"].Value.HtmlDecode();
        if (!relativeUri.Contains("chapters"))
        {
            var bookId = uri.Segments.Last().Split('.')[0];
            relativeUri = $"/chapters/{string.Join("-", bookId.Split('-').Skip(1))}/";
        }

        // Ranobes currently returns an absolute, HTML-encoded URL here. Removing
        // the leading slash made Uri resolve it below /ranobe/, producing the
        // invalid /ranobe/chapters/... address and therefore an empty TOC.
        return uri.MakeRelativeUri(relativeUri);
    }

    private async Task<IEnumerable<UrlChapter>> GetToc(Uri tocUri)
    {
        var doc = await GetDocument(tocUri);
        var lastA = doc.QuerySelector("div.pages a:last-child")?.InnerText;
        var pages = string.IsNullOrWhiteSpace(lastA) ? 1 : int.Parse(lastA);

        Config.Logger.LogInformation("Получаю оглавление");
        var result = new List<UrlChapter>();
        for (var i = 1; i <= pages; i++)
        {
            if (i > 1)
            {
                var pageUri = tocUri.AppendSegment($"page/{i}");
                doc = await GetDocument(pageUri);
            }

            var chapters = doc
                .QuerySelectorAll("#dle-content > .cat_block.cat_line a[title]")
                .Select(a => new UrlChapter(a.Attributes["href"].Value.AsUri(), string.IsNullOrWhiteSpace(a.Attributes["title"].Value) ? "Без названия" : a.Attributes["title"].Value))
                .ToList();

            result.AddRange(chapters);
        }

        Config.Logger.LogInformation($"Получено {result.Count} глав");

        result.Reverse();
        return SliceToc(result, c => c.Title);
    }
}
