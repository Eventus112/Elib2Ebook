using Elib2Ebook.ExternalServices.Jaomix;
using HtmlAgilityPack;

namespace Elib2Ebook.DomainServices.Tests.ExternalServices;

public class JaomixGetterTests
{
    private static HtmlDocument Doc(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    [Fact]
    public void ParsesCurrentTocLayoutAndPageNumbers()
    {
        var doc = Doc("""
            <div class="block-toc">
              <select class="sel-toc">
                <option value="1">Часть 1</option>
                <option value="2">Часть 2</option>
              </select>
            </div>
            <div class="block-toc-out">
              <div class="flex-dow-txt"><div class="title">
                <a href="/book/chapter-2/"><h2>Глава 2</h2></a>
              </div></div>
              <div class="flex-dow-txt"><div class="title">
                <a href="/book/chapter-1/"><h2>Глава 1</h2></a>
              </div></div>
            </div>
            """);
        var bookUrl = new Uri("https://jaomix.ru/book/");

        var chapters = JaomixGetter.ParseChapters(doc, bookUrl).ToList();

        Assert.Equal([1, 2], JaomixGetter.GetTocPageNumbers(doc));
        Assert.Collection(
            chapters,
            chapter => Assert.Equal(("Глава 2", "https://jaomix.ru/book/chapter-2/"), (chapter.Title, chapter.Url.ToString())),
            chapter => Assert.Equal(("Глава 1", "https://jaomix.ru/book/chapter-1/"), (chapter.Title, chapter.Url.ToString())));
    }

    [Fact]
    public void ParsesAjaxTocFragmentWithoutOuterContainer()
    {
        var doc = Doc("""
            <div class="columns-toc">
              <div class="flex-dow-txt"><div class="title">
                <a href="/book/chapter-42/"><h2>Глава 42</h2></a>
              </div></div>
            </div>
            """);

        var chapter = Assert.Single(JaomixGetter.ParseChapters(doc, new Uri("https://jaomix.ru/book/")));

        Assert.Equal("Глава 42", chapter.Title);
        Assert.Equal("https://jaomix.ru/book/chapter-42/", chapter.Url.ToString());
    }

    [Fact]
    public void ExtractChapterUsesCurrentLayoutAndRemovesAds()
    {
        var doc = Doc("""
            <div class="entry-content entry">
              <div class="adblock-service">Реклама</div>
              <div class="lazyblock">Ещё реклама</div>
              <p>Текст главы</p>
              <script>tracking()</script>
            </div>
            """);

        var chapter = JaomixGetter.ExtractChapter(doc);

        Assert.Contains("Текст главы", chapter.DocumentNode.InnerText);
        Assert.DoesNotContain("Реклама", chapter.DocumentNode.InnerText);
        Assert.DoesNotContain("tracking", chapter.DocumentNode.InnerText);
    }

    [Fact]
    public void DetectsCurrentPartialChapterCaptcha()
    {
        var doc = Doc("""
            <div class="entry-content entry">
              <p>Начало главы...</p>
              <div class="but-captcha">Нажмите, для загрузки полной главы.</div>
            </div>
            """);

        Assert.True(JaomixGetter.HasCaptcha(doc));
    }

    [Fact]
    public void UsesEveryCaptchaAngleInTwentyDegreeSteps()
    {
        Assert.Equal(Enumerable.Range(0, 18).Select(value => value * 20), JaomixGetter.GetCaptchaDegrees());
    }

    [Theory]
    [InlineData("<p>Неправильный ответ</p>")]
    [InlineData("<div class=\"block-capth-img\"></div>")]
    [InlineData("<p>Ошибка. Обновите страницу.</p>")]
    public void DetectsCaptchaResponses(string html)
    {
        Assert.True(JaomixGetter.IsCaptchaResponse(html));
    }

    [Fact]
    public void WrapsSolvedCaptchaFragmentAsCurrentChapterLayout()
    {
        var doc = JaomixGetter.WrapChapterFragment("<h2>Глава 0</h2><p>Полный текст главы</p>");

        Assert.False(JaomixGetter.HasCaptcha(doc));
        Assert.Contains("Полный текст главы", JaomixGetter.ExtractChapter(doc).DocumentNode.InnerText);
    }
}
