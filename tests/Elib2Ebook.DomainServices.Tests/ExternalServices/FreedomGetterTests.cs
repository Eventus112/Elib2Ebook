using Elib2Ebook.ExternalServices.Freedom.Getters;
using HtmlAgilityPack;

namespace Elib2Ebook.DomainServices.Tests.ExternalServices;

public class FreedomGetterTests
{
    private static HtmlDocument Doc(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    [Fact]
    public void ExtractsCurrentBookLayout()
    {
        var doc = Doc("""
            <div class="book-img"><img src="/cover.jpg"></div>
            <div class="book-info"><h1>Новая книга</h1></div>
            <div class="chapterlinks">
              <div class="chapterinfo"><a href="/book/chapter-2/">Глава 2</a></div>
              <div class="chapterinfo"><a href="/book/chapter-1/">Глава 1</a></div>
            </div>
            """);
        var bookUrl = new Uri("https://ifreedom.su/ranobe/book/");

        var toc = FreedomGetterBase.ExtractToc(doc, bookUrl);

        Assert.Equal("Новая книга", FreedomGetterBase.GetTitle(doc));
        Assert.Equal("/cover.jpg", FreedomGetterBase.GetCoverPath(doc));
        Assert.Collection(
            toc,
            chapter => Assert.Equal("https://ifreedom.su/book/chapter-1/", chapter.Url.ToString()),
            chapter => Assert.Equal("https://ifreedom.su/book/chapter-2/", chapter.Url.ToString()));
    }

    [Fact]
    public void ExtractChapterUsesCurrentLayoutAndRemovesAds()
    {
        var doc = Doc("""
            <div class="chapter-content">
              <div class="pc-adv">Реклама</div>
              <p>Текст главы</p>
            </div>
            """);

        var chapter = FreedomGetterBase.ExtractChapter(doc);

        Assert.Contains("Текст главы", chapter.DocumentNode.InnerText);
        Assert.DoesNotContain("Реклама", chapter.DocumentNode.InnerText);
    }
}
