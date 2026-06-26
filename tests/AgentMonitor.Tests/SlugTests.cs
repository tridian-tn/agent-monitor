using AgentMonitor.Providers.ClaudeCode.Internal;
using Xunit;

public class SlugTests
{
    [Theory]
    [InlineData(@"D:\TodoListMcp", "D--TodoListMcp")]
    [InlineData(@"D:\ClaudeDesktopMonitor", "D--ClaudeDesktopMonitor")]
    [InlineData(@"C:\Users\tryst", "C--Users-tryst")]
    public void SlugForCwd_matches_claude_layout(string cwd, string expected)
        => Assert.Equal(expected, ClaudePaths.SlugForCwd(cwd));
}
