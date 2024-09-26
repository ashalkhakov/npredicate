using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace RealArtists.NPredicate;

public enum CompoundPredicateType
{
    Not,
    And,
    Or
}

public class CompoundPredicate : Predicate
{
    public CompoundPredicateType CompoundPredicateType { get; set; }

    public IEnumerable<Predicate> Subpredicates { get; set; }

    protected CompoundPredicate() { }

    public static CompoundPredicate And(IEnumerable<Predicate> subpredicates)
    {
        CompoundPredicate p = new()
        {
            CompoundPredicateType = CompoundPredicateType.And,
            Subpredicates = subpredicates
        };
        return p;
    }

    public static CompoundPredicate Or(IEnumerable<Predicate> subpredicates)
    {
        CompoundPredicate p = new()
        {
            CompoundPredicateType = CompoundPredicateType.Or,
            Subpredicates = subpredicates
        };
        return p;
    }

    public static CompoundPredicate Not(Predicate subpredicate)
    {
        CompoundPredicate p = new()
        {
            CompoundPredicateType = CompoundPredicateType.Not,
            Subpredicates =
            [
                subpredicate
            ]
        };
        return p;
    }

    public override string Format
    {
        get
        {
            switch (CompoundPredicateType)
            {
                case CompoundPredicateType.And:
                    return "(" + String.Join(" AND ", Subpredicates) + ")";
                case CompoundPredicateType.Or:
                    return "(" + String.Join(" OR ", Subpredicates) + ")";
                case CompoundPredicateType.Not:
                    return $"NOT ({Subpredicates.First().Format})";
                default:
                    break;
            }
            return "";
        }
    }

    private Expression GenerateAnd(Dictionary<string, Expression> bindings, IEnumerable<Predicate> predicates, LinqDialect dialect)
    {
        if (predicates.Count() > 2)
        {
            var a = predicates.First().LinqExpression(bindings, dialect);
            var b = GenerateAnd(bindings, predicates.Skip(1), dialect);
            return Expression.AndAlso(a, b);
        }
        else if (predicates.Count() == 2)
        {
            var a = predicates.First().LinqExpression(bindings, dialect);
            var b = predicates.Last().LinqExpression(bindings, dialect);
            return Expression.AndAlso(a, b);
        }
        else if (predicates.Count() == 1)
        {
            return predicates.First().LinqExpression(bindings, dialect);
        }
        else
        {
            return new ConstantPredicate(false).LinqExpression(bindings, dialect);
        }
    }

    private Expression GenerateOr(Dictionary<string, Expression> bindings, IEnumerable<Predicate> predicates, LinqDialect dialect)
    {
        if (predicates.Count() > 2)
        {
            Expression a = predicates.First().LinqExpression(bindings, dialect);
            Expression b = GenerateOr(bindings, predicates.Skip(1), dialect);
            return Expression.OrElse(a, b);
        }
        else if (predicates.Count() == 2)
        {
            Expression a = predicates.First().LinqExpression(bindings, dialect);
            Expression b = predicates.Last().LinqExpression(bindings, dialect);
            return Expression.OrElse(a, b);
        }
        else if (predicates.Count() == 1)
        {
            return predicates.First().LinqExpression(bindings, dialect);
        }
        else
        {
            return new ConstantPredicate(false).LinqExpression(bindings, dialect);
        }
    }

    public override Expression LinqExpression(Dictionary<string, Expression> bindings, LinqDialect dialect)
    {
        return CompoundPredicateType switch
        {
            CompoundPredicateType.And => GenerateAnd(bindings, Subpredicates, dialect),
            CompoundPredicateType.Or => GenerateOr(bindings, Subpredicates, dialect),
            CompoundPredicateType.Not => Expression.Not(Subpredicates.First().LinqExpression(bindings, dialect)),
            _ => null,
        };
    }

    public override void Visit(IVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var predicate in Subpredicates)
        {
            predicate.Visit(visitor);
        }
    }
}
