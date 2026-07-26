using PromptRegistry.Core;
using Xunit;

namespace PromptRegistry.Tests;

public class CanonicalHashTests
{
    [Fact]
    public void Same_content_produces_same_hash()
    {
        var a = CanonicalHash.Compute("Hello {{name}}", new[] { "name" }, new Dictionary<string, string> { ["k"] = "v" });
        var b = CanonicalHash.Compute("Hello {{name}}", new[] { "name" }, new Dictionary<string, string> { ["k"] = "v" });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Variable_order_does_not_change_the_hash()
    {
        var a = CanonicalHash.Compute("t", new[] { "x", "y" }, new Dictionary<string, string>());
        var b = CanonicalHash.Compute("t", new[] { "y", "x" }, new Dictionary<string, string>());
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_template_produces_different_hash()
    {
        var a = CanonicalHash.Compute("one", Array.Empty<string>(), new Dictionary<string, string>());
        var b = CanonicalHash.Compute("two", Array.Empty<string>(), new Dictionary<string, string>());
        Assert.NotEqual(a, b);
    }
}

public class PromptReferenceTests
{
    [Theory]
    [InlineData("prompt://checkout-summary@production", "checkout-summary", "production")]
    [InlineData("checkout-summary@staging", "checkout-summary", "staging")]
    public void Parses_valid_references(string input, string name, string env)
    {
        var r = PromptReference.Parse(input);
        Assert.Equal(name, r.Name);
        Assert.Equal(env, r.Environment);
    }

    [Fact]
    public void Round_trips_through_ToString()
    {
        var r = new PromptReference("summary", "production");
        Assert.Equal("prompt://summary@production", r.ToString());
        Assert.Equal(r, PromptReference.Parse(r.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("prompt://@production")]
    [InlineData("prompt://name@")]
    public void Rejects_invalid_references(string input)
        => Assert.False(PromptReference.TryParse(input, out _));
}
