using System.Collections.Generic;
using System.Text;

namespace Froola.Configs.Collections;

public class OptionList<TValue> : List<TValue>
{
    public OptionList()
    {
    }

    public OptionList(IEnumerable<TValue> values) : base(values)
    {
    }
    
    public override string ToString()
    {
        StringBuilder builder = new();

        builder.Append('[');

        var first = true;
        foreach (var value in this)
        {
            if (first)
            {
                builder.Append($"{value}");
                first = false;
            }
            else
            {
                builder.Append($", {value}");
            }
        }

        builder.Append(']');

        return builder.ToString();
    }
}