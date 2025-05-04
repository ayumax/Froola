using System.Collections.Generic;
using System.Text;

namespace Froola.Configs.Collections;

public class OptionDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull
{
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