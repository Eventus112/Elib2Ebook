using Elib2Ebook.ExternalServices.RanobesCom;
using HtmlAgilityPack;

namespace Elib2Ebook.DomainServices.Tests.ExternalServices;

public class RanobesComGetterTests
{
    [Theory]
    [InlineData("https&#58;//ranobes.com/chapters/shadow-slave/", "https://ranobes.com/chapters/shadow-slave/")]
    [InlineData("/chapters/shadow-slave/", "https://ranobes.com/chapters/shadow-slave/")]
    public void GetTocLinkResolvesRanobesLinksFromSiteRoot(string href, string expected)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml($"<div class='r-fullstory-btns'><a title='Перейти в оглавление' href='{href}'>Оглавление</a></div>");

        var actual = RanobesComGetter.GetTocLink(
            doc,
            new Uri("https://ranobes.com/ranobe/317729-shadow-slave.html"));

        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void GetTocLinkFallsBackToBookSlug()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<div class='r-fullstory-btns'><a title='Перейти в оглавление' href='/read/start/'>Оглавление</a></div>");

        var actual = RanobesComGetter.GetTocLink(
            doc,
            new Uri("https://ranobes.com/ranobe/317729-shadow-slave.html"));

        Assert.Equal("https://ranobes.com/chapters/shadow-slave/", actual.ToString());
    }

    [Fact]
    public void ExtractChapterPreservesTextNodesAndRemovesAds()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(
            "<div id='arrticle'>Первый абзац<br><br>Второй абзац" +
            "<div align='center'><script>window.yaContextCb=[]</script>Реклама</div>" +
            "<div class='splitnewsnavigation'>Навигация</div></div>");

        var chapter = RanobesComGetter.ExtractChapter(doc);

        Assert.Contains("Первый абзац", chapter.DocumentNode.InnerText);
        Assert.Contains("Второй абзац", chapter.DocumentNode.InnerText);
        Assert.DoesNotContain("Реклама", chapter.DocumentNode.InnerText);
        Assert.DoesNotContain("Навигация", chapter.DocumentNode.InnerText);
        Assert.Equal(2, chapter.DocumentNode.SelectNodes("//br").Count);
    }
}
