using System;
using System.Collections.Generic;
using Xunit;

namespace Yafc.Model.Tests;

public class SearchQueryTests {
    [Fact]
    public void Tokens_AreReadOnly() {
        SearchQuery query = new("iron plate");

        Assert.Equal(new[] { "iron", "plate" }, query.tokens);
        Assert.False(query.tokens is string[]);

        IList<string> tokens = Assert.IsAssignableFrom<IList<string>>(query.tokens);
        Assert.True(tokens.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => tokens[0] = "copper");
        Assert.True(query.Match("iron plate"));
        Assert.False(query.Match("copper plate"));
    }

    [Fact]
    public void DefaultQuery_HasEmptyTokens() {
        SearchQuery query = default;

        Assert.True(query.empty);
        Assert.Empty(query.tokens);
        Assert.True(query.Match("anything"));
    }
}
