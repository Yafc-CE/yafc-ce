using System;
using System.Collections.Generic;

namespace Yafc.Model;

public readonly struct SearchQuery(string query) {
    private static readonly IReadOnlyList<string> emptyTokens = Array.AsReadOnly(Array.Empty<string>());
    private readonly IReadOnlyList<string>? _tokens = string.IsNullOrWhiteSpace(query)
        ? emptyTokens
        : Array.AsReadOnly(query.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public readonly string query = query;
    public readonly IReadOnlyList<string> tokens => _tokens ?? emptyTokens;
    public readonly bool empty => tokens.Count == 0;

    public bool Match(string? text) {
        if (text == null) {
            return false;
        }

        if (empty) {
            return true;
        }

        foreach (string token in tokens) {
            if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) {
                return false;
            }
        }

        return true;
    }
}
