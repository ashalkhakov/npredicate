using System.Collections.Generic;
using System.Linq.Expressions;

namespace RealArtists.NPredicate;

public class ConstantPredicate(bool val) : Predicate
{
    public bool Value { get; set; } = val;

    public override string Format
    {
        get
        {
            return Value ? "TRUE" : "FALSE";
        }
    }

    public override Expression LinqExpression(Dictionary<string, Expression> bindings, LinqDialect dialect)
    {
        return Expression.Constant(Value);
    }
}
