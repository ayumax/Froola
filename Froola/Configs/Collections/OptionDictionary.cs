using System.Collections.Generic;
using System.Text;

namespace Froola.Configs.Collections;

public class OptionDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull
{
    public OptionDictionary()
    {
    }

    public OptionDictionary(IEnumerable<KeyValuePair<TKey, TValue>> pairs) 
    {
    }

    public override string ToString()
    {
        StringBuilder builder = new();

        builder.Append('[');

        var first = true;
        foreach (var keyValue in this)
        {
            if (first)
            {
                builder.Append($"{{{keyValue.Key}, {keyValue.Value}}}");
                first = false;
            }
            else
            {
                builder.Append($", {{{keyValue.Key}, {keyValue.Value}}}");
            }
        }

        builder.Append(']');

        return builder.ToString();
    }
}

public class OptionDictionary : OptionDictionary<string, string>
{
    public OptionDictionary()
    {
    }

    public OptionDictionary(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        foreach (var pair in pairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                Add(pair.Key, pair.Value);
            }
        }
    }
}