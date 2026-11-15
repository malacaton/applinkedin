using Ali.Core.Content;

namespace Ali.Core.Tests.Content;

public class TitleSlugTests
{
    // ---------- CA-3 ----------

    [Theory]
    [InlineData("Qué aprendimos del RAG", "que-aprendimos-del-rag")]
    [InlineData("Año nuevo: ¿pingüinos o ÑANDÚES?", "ano-nuevo-pinguinos-o-nandues")]
    [InlineData("  --Hola,   mundo!!  ", "hola-mundo")]
    [InlineData("C# y .NET 10", "c-y-net-10")]
    [InlineData("Agentes 🤖 en producción", "agentes-en-produccion")]
    [InlineData("¿?¡!", "")]
    [InlineData("", "")]
    public void From_ConvertsTitle(string title, string expected)
    {
        Assert.Equal(expected, TitleSlug.From(title));
    }

    [Fact]
    public void From_LongTitle_IsCutWithoutTrailingHyphen()
    {
        // 59 letters, a space and more words: the cut falls right after the hyphen.
        var title = new string('a', 59) + " bcd";

        var slug = TitleSlug.From(title);

        Assert.Equal(new string('a', 59), slug);
    }

    [Fact]
    public void From_LongTitle_IsAtMostMaxLength()
    {
        var slug = TitleSlug.From(string.Join(' ', Enumerable.Repeat("palabra", 30)));

        Assert.True(slug.Length <= TitleSlug.MaxLength);
        Assert.False(slug.EndsWith('-'));
    }
}
