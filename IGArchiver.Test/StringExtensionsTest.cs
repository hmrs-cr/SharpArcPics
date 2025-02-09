using PicArchiver.Core.Metadata;
using PicArchiver.Extensions;

namespace IGArchiver.Test;

[TestFixture]
public class StringExtensionsTest
{
    [Test]
    public void ResolveTokensTest()
    {
        const string template = "This is a {TEST}. A {TYPE} test. Yes it is {TYPE} ({NO}). This is a number: {NUMBER}";
        var values = new Dictionary<string, object?>
        {
            { "TYPE", "nice" },
            { "TEST", "test" },
            { "NO", "yes" },
            { "NUMBER", 1983 },
        };
        
        var result = template.ResolveTokens(new FileMetadata(values));
        Assert.That(result, Is.EqualTo("This is a test. A nice test. Yes it is nice (yes). This is a number: 1983"));
    }
}