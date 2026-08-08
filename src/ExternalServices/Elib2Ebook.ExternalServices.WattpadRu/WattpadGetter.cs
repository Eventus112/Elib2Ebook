using System.Net.Http.Json;
using Elib2Ebook.Domain.Book;
using Elib2Ebook.Domain.Common;
using Elib2Ebook.DomainServices.Configs;
using Elib2Ebook.DomainServices.Extensions;
using Elib2Ebook.DomainServices.Getters;
using Elib2Ebook.ExternalServices.WattpadRu.Types;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.Logging;

namespace Elib2Ebook.ExternalServices.WattpadRu;

public class WattpadGetter(BookGetterConfig config) : GetterBase(config)
{
    protected override Uri SystemUrl => new("https://watt-pad.ru/");

    protected override string GetId(Uri url) => url.GetSegment(2);

    public override async Task<Book> Get(Uri url)
    {
        var id = GetId(url);
        url = SystemUrl.MakeRelativeUri($"/story/{id}");
        var wattpadInfo = await Config.Client.GetFromJsonAsync<WattpadInfo>(SystemUrl.MakeRelativeUri($"/api/offline/stories/{id}"));

        var book = new Book(url)
        {
            Cover = await GetCover(wattpadInfo),
            Chapters = await FillChapters(wattpadInfo),
            Title = wattpadInfo.Story.Name,
            Author = GetAuthor(wattpadInfo),
            CoAuthors = GetCoAuthors(wattpadInfo),
            Annotation = wattpadInfo.Story.Description
        };

        return book;
    }

    private Author GetAuthor(WattpadInfo wattpadInfo)
    {
        var author = wattpadInfo.Story.Authors.First();
        return new Author(author.Name, SystemUrl.MakeRelativeUri($"/author/{author.Slug}"));
    }

    private IEnumerable<Author> GetCoAuthors(WattpadInfo wattpadInfo)
    {
        return wattpadInfo.Story.Authors
            .Skip(1)
            .Select(author => new Author(author.Name, SystemUrl.MakeRelativeUri($"/author/{author.Slug}"))).ToList();
    }

    private async Task<IEnumerable<Chapter>> FillChapters(WattpadInfo wattpadInfo)
    {
        var result = new List<Chapter>();
        if (Config.Options.NoChapters)
        {
            return result;
        }

        foreach (var wattpadChapter in SliceToc(wattpadInfo.Parts, c => c.Chapter))
        {
            Config.Logger.LogInformation($"Загружаю главу {wattpadChapter.Chapter.CoverQuotes()}");
            var chapter = new Chapter();

            var chapterDoc = GetChapter(wattpadChapter);
            chapter.Images = await GetImages(chapterDoc, SystemUrl);
            chapter.Content = chapterDoc.DocumentNode.InnerHtml;
            chapter.Title = wattpadChapter.Chapter;

            result.Add(chapter);
        }

        return result;
    }

    private static HtmlDocument GetChapter(WattpadPart part)
    {
        var doc = part.Content.AsHtmlDoc();
        foreach (var node in doc.QuerySelectorAll("p"))
        {
            node.Attributes.RemoveAll();
        }

        return doc;
    }

    private Task<TempFile> GetCover(WattpadInfo wattpadInfo)
    {
        return !string.IsNullOrWhiteSpace(wattpadInfo.Story.Cover) ? SaveImage(wattpadInfo.Story.Cover.AsUri()) : Task.FromResult(default(TempFile));
    }
}
