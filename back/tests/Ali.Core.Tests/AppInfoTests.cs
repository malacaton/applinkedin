using Ali.Core;

namespace Ali.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsAppLinkedIn()
    {
        Assert.Equal("appLinkedIn", AppInfo.Name);
    }
}
