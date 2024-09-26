﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace RealArtists.NPredicate;

class Utils
{

    public static Expression CallSafe(LinqDialect dialect, Expression target, string methodName, params Expression[] arguments)
    {
        if ((dialect & LinqDialect.EntityFramework) == 0)
        {
            var defaultTarget = Expression.Default(target.Type);
            var isNull = Expression.ReferenceEqual(target, defaultTarget);
            var argTypes = arguments.Select(a => a.Type).ToArray();
            var called = Expression.Call(target, target.Type.GetMethod(methodName, argTypes), arguments);
            var defaultCalled = Expression.Default(called.Type);
            return Expression.Condition(isNull, defaultCalled, called);
        }
        else
        {
            var argTypes = arguments.Select(a => a.Type).ToArray();
            return Expression.Call(target, target.Type.GetMethod(methodName, argTypes), arguments);
        }
    }

    public static Expression CallAggregate(string aggregate, params Expression[] args)
    {
        Debug.Assert(args.Length > 0);
        List<Type> types = [.. args.Select(e => e.Type)];
        var aggregateMethod = typeof(System.Linq.Enumerable).GetMethod(aggregate, [.. types]);
        if (aggregateMethod == null)
        {
            List<Type> typeArguments = [ElementType(types[0])];
            types[0] = typeof(IEnumerable<>);
            for (var i = 1; i < types.Count; i++)
            {
                if (types[i].IsGenericType)
                {
                    foreach (var specific in types[i].GetGenericArguments())
                    {
                        if (!typeArguments.Contains(specific))
                        {
                            typeArguments.Add(specific);
                        }
                    }
                    types[i] = types[i].GetGenericTypeDefinition();
                }
            }
            var aggregateOpenMethod = GetGenericMethod(typeof(System.Linq.Enumerable), aggregate, [.. types]);
            aggregateMethod = aggregateOpenMethod.MakeGenericMethod(typeArguments.Take(aggregateOpenMethod.GetGenericArguments().Length).ToArray());
        }
        return Expression.Call(aggregateMethod, args);
    }

    public static Type ElementType(Type enumerableType)
    {
        if (enumerableType.GetGenericArguments().Length > 0)
        {
            return enumerableType.GetGenericArguments()[0];
        }
        else
        {
            return enumerableType.GetElementType();
        }
    }

    public static bool TypeIsEnumerable(Type type)
    {
        // opt out some technically enumerable types that we don't treat as such because
        // they aren't enumerable in Cocoa
        if (type == typeof(string))
        {
            return false;
        }

        // http://stackoverflow.com/questions/1121834/finding-out-if-a-type-implements-a-generic-interface

        // this conditional is necessary if myType can be an interface,
        // because an interface doesn't implement itself: for example,
        // typeof (IList<int>).GetInterfaces () does not contain IList<int>!
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return true;
        }

        foreach (var i in type.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return true;
            }
        }

        return false;
    }

    public static Expression CallMath(string fn, params Expression[] args)
    {
        var mathMethod = typeof(System.Math).GetMethod(fn, args.Select(x => x.Type).ToArray());
        return Expression.Call(mathMethod, args);
    }

    private static readonly Func<MethodInfo, IEnumerable<Type>> ParameterTypeProjection =
        method => method.GetParameters()
            .Select(p => p.ParameterType.ContainsGenericParameters ? p.ParameterType.GetGenericTypeDefinition() : p.ParameterType);

    public static MethodInfo GetGenericMethod(Type type, string name, params Type[] parameterTypes)
    {
        foreach (var method in type.GetMethods())
        {
            if (method.Name == name)
            {
                var projection = ParameterTypeProjection(method);
                if (parameterTypes.SequenceEqual(projection))
                {
                    return method;
                }
            }
        }
        return null;

#if false
        return (from method in type.GetMethods()
        where method.Name == name
        where parameterTypes.SequenceEqual(ParameterTypeProjection(method))
        select method).SingleOrDefault();
#endif
    }

    public static bool IsTypeNumeric(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
            _ => false,
        };
    }

    public static DateTime GetReferenceDate()
    {
        DateTime reference = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return reference;
    }

    public static double TimeIntervalSinceReferenceDate(DateTime dateTime)
    {
        var reference = GetReferenceDate();
        var span = dateTime - reference;
        return span.TotalSeconds;
    }

    public static DateTime DateTimeFromTimeIntervalSinceReferenceDate(double timeInterval)
    {
        var reference = GetReferenceDate();
        var span = TimeSpan.FromSeconds(timeInterval);
        var dateTime = reference + span;
        return dateTime;
    }

    public static Expression AsDouble(Expression a)
    {
        if (a.Type == typeof(double))
        {
            return a;
        }
        else
        {
            return Expression.Convert(a, typeof(double));
        }
    }

    public static Expression AsInt(Expression a)
    {
        if (a.Type == typeof(int))
        {
            return a;
        }
        else
        {
            return Expression.Convert(a, typeof(int));
        }
    }

    public static bool IsNullableValueType(Type t)
    {
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static Expression AsNullableValueType(Expression e)
    {
        if (IsNullableValueType(e.Type))
        {
            return e;
        }
        if (!e.Type.IsValueType)
        {
            throw new ArgumentException("t must be a value type");
        }
        return Utils.AsNullable(e);
    }

    public static Type ValueTypeInNullable(Type nullable)
    {
        if (!IsNullableValueType(nullable))
        {
            return nullable;
        }
        return nullable.GetGenericArguments()[0];
    }

    public static bool IsNullConstant(Expression e)
    {
        return e is ConstantExpression expression && expression.Value == null;
    }

    public static Expression NullForValueType(Type valueType)
    {
        if (!valueType.IsValueType)
        {
            throw new ArgumentException("valueType must be a value type");
        }
        var type = typeof(Nullable<>).MakeGenericType(valueType);
        return Expression.Constant(null, type);
    }

    public static Expression AsNullable(Expression a)
    {
        if (a.Type.IsByRef)
        {
            return a;
        }
        else if (a.Type.IsGenericType && a.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return a;
        }
        else
        {
            var type = typeof(Nullable<>).MakeGenericType(a.Type);
            return Expression.Convert(a, type);
        }
    }

    public static Expression AsNotNullable(Expression a)
    {
        if (a.Type.IsGenericType && a.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var type = a.Type.GetGenericArguments()[0];
            return Expression.Convert(a, type);
        }
        else
        {
            return a;
        }
    }
}

