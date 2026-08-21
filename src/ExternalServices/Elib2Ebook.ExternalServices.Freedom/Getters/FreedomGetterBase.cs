using Elib2Ebook.Domain.Book;
using Elib2Ebook.Domain.Common;
using Elib2Ebook.DomainServices.Configs;
using Elib2Ebook.DomainServices.Extensions;
using Elib2Ebook.DomainServices.Getters;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.Logging;

namespace Elib2Ebook.ExternalServices.Freedom.Getters;

public abstract class FreedomGetterBase(BookGetterConfig config) : GetterBase(config)
{
    public override async Task<Book> Get(Uri url)
    {
        url = await GetMainUrl(url);
        url = SystemUrl.MakeRelativeUri($"/ranobe/{GetId(url)}/");
        var doc = await Config.Client.GetHtmlDocWithTriesAsync(url);
        var title = GetTitle(doc);

        var book = new Book(url)
        {
            Cover = await GetCover(doc, url), Chapters = await FillChapters(doc, url), Title = title, Author = GetAuthor()
        };

        return book;
    }

    private async Task<Uri> GetMainUrl(Uri url)
    {
        if (url.GetSegment(1) == "ranobe")
        {
            return url;
        }

        var doc = await Config.Client.GetHtmlDocWithTriesAsync(url);
        var bookLink = doc.QuerySelector("div.bun2 a") ??
                       doc.QuerySelector(".chapter-setting .copyaddchapset a[href*='/ranobe/']");
        var href = bookLink?.Attributes["href"]?.Value;
        if (string.IsNullOrWhiteSpace(href))
        {
            throw new InvalidOperationException($"На странице Freedom не найдена ссылка на книгу: {url}");
        }

        return url.MakeRelativeUri(href);
    }

    private IEnumerable<UrlChapter> GetToc(HtmlDocument doc, Uri url)
    {
        var result = ExtractToc(doc, url);
        if (result.Count == 0)
        {
            throw new InvalidOperationException("На странице Freedom не найдено оглавление книги");
        }

        return SliceToc(result, c => c.Title);
    }

    internal static List<UrlChapter> ExtractToc(HtmlDocument doc, Uri url)
    {
        var links = doc.QuerySelectorAll("div.li-col1-ranobe > a").ToList();
        if (links.Count == 0)
        {
            links = doc.QuerySelectorAll(".chapterlinks .chapterinfo > a").ToList();
        }

        return links
            .Where(a => !string.IsNullOrWhiteSpace(a.Attributes["href"]?.Value))
            .Select(a => new UrlChapter(url.MakeRelativeUri(a.Attributes["href"].Value), a.GetText()))
            .Reverse()
            .ToList();
    }

    internal static string GetTitle(HtmlDocument doc)
    {
        var title = doc.QuerySelector("h1.entry-title")?.GetText() ??
                    doc.QuerySelector(".book-info h1")?.GetText();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("На странице Freedom не найдено название книги");
        }

        return title.Trim();
    }

    private async Task<IEnumerable<Chapter>> FillChapters(HtmlDocument doc, Uri uri)
    {
        var result = new List<Chapter>();
        if (Config.Options.NoChapters)
        {
            return result;
        }

        foreach (var urlChapter in GetToc(doc, uri))
        {
            var chapter = new Chapter
            {
                Title = urlChapter.Title
            };

            Config.Logger.LogInformation($"Загружаю главу {urlChapter.Title.CoverQuotes()}");

            var chapterDoc = await GetChapter(urlChapter.Url);
            if (chapterDoc != null)
            {
                chapter.Images = await GetImages(chapterDoc, urlChapter.Url);
                chapter.Content = chapterDoc.DocumentNode.InnerHtml;
            }

            result.Add(chapter);
        }

        return result;
    }

    private async Task<HtmlDocument> GetChapter(Uri url)
    {
        var doc = await Config.Client.GetHtmlDocWithTriesAsync(url);
        return ExtractChapter(doc, url);
    }

    internal static HtmlDocument ExtractChapter(HtmlDocument doc, Uri url = null)
    {
        var content = doc.QuerySelector("div.entry-content") ?? doc.QuerySelector("div.chapter-content");
        if (content == null)
        {
            throw new InvalidOperationException($"На странице Freedom не найден текст главы{(url == null ? string.Empty : $": {url}")}");
        }

        var notice = content.QuerySelector("div.single-notice");
        return notice?.GetText() == "Для чтения купите главу." ?
            null :
            content.InnerHtml.AsHtmlDoc().RemoveNodes("div[class*=adv]");
    }

    private static Author GetAuthor()
    {
        return new Author("Ifreedom");
    }

    private Task<TempFile> GetCover(HtmlDocument doc, Uri uri)
    {
        var imagePath = GetCoverPath(doc);
        return !string.IsNullOrWhiteSpace(imagePath) ? SaveImage(uri.MakeRelativeUri(imagePath)) : Task.FromResult(default(TempFile));
    }

    internal static string GetCoverPath(HtmlDocument doc)
        => (doc.QuerySelector("div.img-ranobe img") ?? doc.QuerySelector(".book-img img"))?.Attributes["src"]?.Value;
}
