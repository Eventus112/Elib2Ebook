using System.Text;
using Elib2Ebook.Domain.Book;
using Elib2Ebook.Domain.Common;
using Elib2Ebook.DomainServices.Configs;
using Elib2Ebook.DomainServices.Extensions;
using Elib2Ebook.DomainServices.Getters;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.Logging;

namespace Elib2Ebook.ExternalServices.Jaomix;

public class JaomixGetter(BookGetterConfig config) : GetterBase(config)
{
    protected override Uri SystemUrl => new("https://jaomix.ru/");

    protected override string GetId(Uri url)
        => url.GetSegment(1);

    public override async Task<Book> Get(Uri url)
    {
        url = SystemUrl.MakeRelativeUri($"/{GetId(url)}/");
        var doc = await Config.Client.GetHtmlDocWithTriesAsync(url);

        var book = new Book(url)
        {
            Cover = await GetCover(doc, url), Chapters = await FillChapters(doc, url), Title = doc.GetTextBySelector("h1"), Author = new Author("Jaomix")
        };

        return book;
    }

    private async Task<IEnumerable<Chapter>> FillChapters(HtmlDocument doc, Uri url)
    {
        var result = new List<Chapter>();
        if (Config.Options.NoChapters)
        {
            return result;
        }

        foreach (var jaomixChapter in await GetToc(doc, url))
        {
            Config.Logger.LogInformation($"Загружаю главу {jaomixChapter.Title.CoverQuotes()}");
            var chapter = new Chapter();
            var chapterDoc = await GetChapter(jaomixChapter.Url);
            chapter.Images = await GetImages(chapterDoc, jaomixChapter.Url);
            chapter.Content = chapterDoc.DocumentNode.InnerHtml;
            chapter.Title = jaomixChapter.Title;

            result.Add(chapter);
            await Task.Delay(500);
        }

        return result;
    }

    private async Task<HtmlDocument> GetChapter(Uri url)
    {
        var doc = await Config.Client.GetHtmlDocWithTriesAsync(url);
        if (HasCaptcha(doc))
        {
            Config.Logger.LogInformation($"Jaomix запросил поворот изображения для {url}. Прохожу проверку автоматически");
            doc = await SolveCaptcha(url);
        }

        if (HasCaptcha(doc))
        {
            throw new InvalidOperationException($"Jaomix не принял автоматическую проверку для {url}. Повторите загрузку позже");
        }

        return ExtractChapter(doc, url);
    }

    private async Task<HtmlDocument> SolveCaptcha(Uri chapterUrl)
    {
        await PostCaptcha(chapterUrl, new Dictionary<string, string>
        {
            ["action"] = "picturecaptcharotate"
        });

        foreach (var degree in GetCaptchaDegrees())
        {
            var html = await PostCaptcha(chapterUrl, new Dictionary<string, string>
            {
                ["action"] = "piccapt",
                ["deg"] = degree.ToString()
            });

            if (IsCaptchaResponse(html))
            {
                continue;
            }

            Config.Logger.LogInformation($"Jaomix принял проверку на угле {degree}°");
            return WrapChapterFragment(html);
        }

        throw new InvalidOperationException($"Jaomix не принял ни один угол поворота для {chapterUrl}. Повторите загрузку позже");
    }

    private async Task<string> PostCaptcha(Uri chapterUrl, Dictionary<string, string> form)
    {
        using var response = await Config.Client.SendWithTriesAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, SystemUrl.MakeRelativeUri("/wp-admin/admin-ajax.php"));
            request.Headers.Referrer = chapterUrl;
            request.Content = new FormUrlEncodedContent(form);
            return request;
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    internal static IEnumerable<int> GetCaptchaDegrees()
        => Enumerable.Range(0, 18).Select(value => value * 20);

    internal static bool IsCaptchaResponse(string html)
        => string.IsNullOrWhiteSpace(html) ||
           html.Contains("Неправильный ответ", StringComparison.OrdinalIgnoreCase) ||
           html.Contains("Обновить капчу", StringComparison.OrdinalIgnoreCase) ||
           html.Contains("Ошибка. Обновите страницу", StringComparison.OrdinalIgnoreCase) ||
           html.Contains("block-capth-img", StringComparison.OrdinalIgnoreCase);

    internal static HtmlDocument WrapChapterFragment(string html)
        => $"<div class=\"entry-content entry\">{html}</div>".AsHtmlDoc();

    internal static bool HasCaptcha(HtmlDocument doc)
        => doc.QuerySelector("div.themeform div.h-captcha, div.themeform div.but-captcha, .entry-content .h-captcha, .entry-content .but-captcha") != null;

    internal static HtmlDocument ExtractChapter(HtmlDocument doc, Uri url = null)
    {
        var content = doc.QuerySelector("div.themeform") ?? doc.QuerySelector(".entry-content.entry");
        if (content == null)
        {
            throw new InvalidOperationException($"На странице Jaomix не найден текст главы{(url == null ? string.Empty : $": {url}")}");
        }

        var sb = new StringBuilder();

        foreach (var node in content.ChildNodes)
        {
            var nodeClass = node.Attributes["class"]?.Value;
            if (node.Name != "br" &&
                node.Name != "script" &&
                node.Name != "style" &&
                !string.IsNullOrWhiteSpace(node.InnerHtml) &&
                nodeClass?.Contains("adblock-service") != true &&
                nodeClass?.Contains("lazyblock") != true)
            {
                var tag = node.Name == "#text" ? "p" : node.Name;
                sb.Append(node.InnerHtml.HtmlDecode().CoverTag(tag));
            }
        }

        return sb.AsHtmlDoc();
    }

    private async Task<IEnumerable<UrlChapter>> GetToc(HtmlDocument doc, Uri url)
    {
        var chapters = ParseChapters(doc, url).ToList();
        var pages = GetTocPageNumbers(doc);
        foreach (var page in pages.Where(page => page > 1))
        {
            Config.Logger.LogInformation($"Загружаю оглавление Jaomix: часть {page} из {pages.Count}");
            var pageDoc = await GetTocPage(url, page);
            chapters.AddRange(ParseChapters(pageDoc, url));
        }

        chapters = chapters
            .Where(chapter => chapter.Url != null)
            .GroupBy(chapter => chapter.Url.AbsoluteUri)
            .Select(group => group.First())
            .Reverse()
            .ToList();

        if (chapters.Count == 0)
        {
            throw new InvalidOperationException("На странице Jaomix не найдено оглавление книги");
        }

        Config.Logger.LogInformation($"Получено {chapters.Count} глав");

        return SliceToc(chapters, c => c.Title);
    }

    private async Task<HtmlDocument> GetTocPage(Uri bookUrl, int page)
    {
        using var response = await Config.Client.SendWithTriesAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, SystemUrl.MakeRelativeUri("/wp-admin/admin-ajax.php"));
            request.Headers.Referrer = bookUrl;
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["action"] = "loadpagenavchapstt",
                ["page"] = page.ToString()
            });
            return request;
        });
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        return stream.AsHtmlDoc(Encoding.UTF8);
    }

    private Task<TempFile> GetCover(HtmlDocument doc, Uri bookUri)
    {
        var imagePath = doc.QuerySelector("div.img-book img")?.Attributes["src"]?.Value;
        return !string.IsNullOrWhiteSpace(imagePath) ? SaveImage(bookUri.MakeRelativeUri(imagePath)) : Task.FromResult(default(TempFile));
    }

    internal static List<int> GetTocPageNumbers(HtmlDocument doc)
    {
        var pages = doc.QuerySelectorAll(".block-toc select.sel-toc option")
            .Select(option => int.TryParse(option.Attributes["value"]?.Value, out var page) ? page : 0)
            .Where(page => page > 0)
            .Distinct()
            .OrderBy(page => page)
            .ToList();

        return pages.Count > 0 ? pages : [1];
    }

    internal static IEnumerable<UrlChapter> ParseChapters(HtmlDocument doc, Uri url)
    {
        var links = doc.QuerySelectorAll("form.download-chapter .hiddenstab .flex-dow-txt a").ToList();
        if (links.Count == 0)
        {
            links = doc.QuerySelectorAll(".block-toc-out .flex-dow-txt a").ToList();
        }
        if (links.Count == 0)
        {
            links = doc.QuerySelectorAll(".flex-dow-txt a").ToList();
        }

        return links
            .Where(link => !string.IsNullOrWhiteSpace(link.Attributes["href"]?.Value))
            .Select(link => new UrlChapter(url.MakeRelativeUri(link.Attributes["href"].Value), link.GetText().Trim()));
    }
}
