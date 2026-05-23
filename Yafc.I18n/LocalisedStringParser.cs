using System.Text;

namespace Yafc.I18n;

public static class LocalisedStringParser {
    public static string? ParseObject(object localisedString) {
        try {
            return RemoveRichText(ParseStringOrArray(localisedString));
        }
        catch {
            return null;
        }
    }

    /// <summary>
    /// Creates the localized string for the supplied key, using <paramref name="parameters"/> for substitutions.
    /// </summary>
    /// <param name="key">The UI key to load.</param>
    /// <param name="parameters">The substitution parameters to be used.</param>
    /// <returns>The localized string for <paramref name="key"/>, using <paramref name="parameters"/> for substitutions.</returns>
    public static string? ParseKey(string key, object[] parameters) {
        try {
            return RemoveRichText(ParseKeyInternal(key, parameters));
        }
        catch {
            return null;
        }
    }

    private static string? ParseStringOrArray(object? obj) {
        if (obj is ILocalizable table && table.Get(out string? key, out object[]? parameters)) {
            return ParseKeyInternal(key, parameters);
        }

        return obj?.ToString();
    }


    private static string? ParseKeyInternal(string key, object?[] parameters) {
        if (key == "") {
            StringBuilder builder = new StringBuilder();
            foreach (object? subString in parameters) {
                string? localisedSubString = ParseStringOrArray(subString);
                if (localisedSubString == null) {
                    return null;
                }

                builder.Append(localisedSubString);
            }

            return builder.ToString();
        }
        else if (key == "?") {
            foreach (object? alternative in parameters) {
                string? localisedAlternative = ParseStringOrArray(alternative);
                if (localisedAlternative != null) {
                    return localisedAlternative;
                }
            }

            return null;
        }
        else if (FactorioLocalization.Localize(key) is { } localisedString) {
            string?[] localisedParameters = [.. parameters.Select(ParseStringOrArray)];
            return ReplaceBuiltInParameters(localisedString, localisedParameters);
        }

        return null;
    }

    public static string? ReplaceBuiltInParameters(string format, string?[] parameters) {
        if (!format.Contains("__")) {
            return format;
        }

        StringBuilder result = new StringBuilder();
        int cursor = 0;
        while (true) {
            int start = format.IndexOf("__", cursor);
            if (start == -1) {
                result.Append(format[cursor..]);
                return result.ToString();
            }
            if (start > cursor) {
                result.Append(format[cursor..start]);
            }

            int end = format.IndexOf("__", start + 2);
            string type = format[(start + 2)..end];
            switch (type) {
                case "CONTROL_STYLE_BEGIN":
                case "CONTROL_STYLE_END":
                case "REMARK_COLOR_BEGIN":
                case "REMARK_COLOR_END":
                    break;
                case "CONTROL_LEFT_CLICK":
                case "CONTROL_RIGHT_CLICK":
                case "CONTROL_KEY_SHIFT":
                case "CONTROL_KEY_CTRL":
                case "CONTROL_MOVE":
                    result.Append(format[start..(end + 2)]);
                    break;
                case "CONTROL":
                case "CONTROL_MODIFIER":
                case "ALT_CONTROL_LEFT_CLICK":
                case "ALT_CONTROL_RIGHT_CLICK":
                    readExtraParameter();
                    result.Append(format[start..(end + 2)]);
                    break;
                case "ALT_CONTROL":
                    readExtraParameter();
                    readExtraParameter();
                    result.Append(format[start..(end + 2)]);
                    break;
                case "ENTITY":
                case "ITEM":
                case "TILE":
                case "FLUID":
                case "RECIPE":
                    string name = readExtraParameter();
                    result.Append(ParseKeyInternal($"{type.ToLower()}-name.{name}", []));
                    break;
                case "PLANET":
                    name = readExtraParameter();
                    result.Append(ParseKeyInternal($"space-location-name.{name}", []));
                    break;
                case "YAFC":
                    name = readExtraParameter();
                    result.Append(ParseKeyInternal("yafc." + name, parameters));
                    break;
                case "plural_for_parameter":
                    string deciderIdx = readExtraParameter();
                    string? decider = parameters[int.Parse(deciderIdx) - 1];
                    if (decider == null) {
                        return null;
                    }

                    var plurals = readPluralOptions();
                    string? selected = selectPluralOption(decider, plurals);
                    if (selected == null) {
                        return null;
                    }

                    string? innerReplaced = ReplaceBuiltInParameters(selected, parameters);
                    if (innerReplaced == null) {
                        return null;
                    }

                    result.Append(innerReplaced);
                    break;
                default:
                    if (int.TryParse(type, out int idx) && idx >= 1 && idx <= parameters.Length) {
                        string? referencedParameter = parameters[idx - 1];
                        if (referencedParameter == null) {
                            return null;
                        }

                        result.Append(referencedParameter);
                    }
                    else {
                        result.Append(format[start..(end + 2)]);
                    }

                    break;
            }
            cursor = end + 2;

            string readExtraParameter() {
                int end2 = format.IndexOf("__", end + 2);
                string result = format[(end + 2)..end2];
                end = end2;
                return result;
            }

            (Func<string, bool> Pattern, string Result)[] readPluralOptions() {
                int end2 = format.IndexOf("}__", end + 3);
                string[] options = format[(end + 3)..end2].Split('|');
                end = end2 + 1;
                return [.. options.Select(readPluralOption)];
            }

            (Func<string, bool> Pattern, string Result) readPluralOption(string option) {
                string[] sides = option.Split('=');
                if (sides.Length != 2) {
                    throw new FormatException($"Invalid plural format: {option}");
                }

                string pattern = sides[0];
                string result = sides[1];
                string[] alternatives = pattern.Split(',');
                return (x => alternatives.Any(a => match(a, x)), result);
            }

            string? selectPluralOption(string decider, (Func<string, bool> Pattern, string Result)[] options) {
                foreach (var option in options) {
                    if (option.Pattern(decider)) {
                        return option.Result;
                    }
                }

                return null;
            }

            static bool match(string pattern, string text) {
                const string ends_in_prefix = "ends in ";
                if (pattern == "rest") {
                    return true;
                }
                else if (pattern.StartsWith(ends_in_prefix)) {
                    return text.EndsWith(pattern[ends_in_prefix.Length..]);
                }
                else {
                    return text == pattern;
                }
            }
        }
    }

    private static string? RemoveRichText(string? text) {
        if (text == null) {
            return null;
        }

        StringBuilder localeBuilder = new StringBuilder(text);
        _ = localeBuilder.Replace("\\n", "\n");

        // Cleaning up tags using simple state machine
        // 0 = outside of tag, 1 = first potential tag char, 2 = inside possible tag, 3 = inside definite tag
        // tag is definite when it contains '=' or starts with '/' or '.'
        int state = 0, tagStart = 0;
        for (int i = 0; i < localeBuilder.Length; i++) {
            char chr = localeBuilder[i];

            switch (state) {
                case 0:
                    if (chr == '[') {
                        state = 1;
                        tagStart = i;
                    }
                    break;
                case 1:
                    if (chr == ']') {
                        state = 0;
                    }
                    else {
                        state = (chr is '/' or '.') ? 3 : 2;
                    }

                    break;
                case 2:
                    if (chr == '=') {
                        state = 3;
                    }
                    else if (chr == ']') {
                        state = 0;
                    }

                    break;
                case 3:
                    if (chr == ']') {
                        _ = localeBuilder.Remove(tagStart, i - tagStart + 1);
                        i = tagStart - 1;
                        state = 0;
                    }
                    break;
            }
        }

        return localeBuilder.ToString();
    }
}
