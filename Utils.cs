﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

namespace Predicate
{
    class Utils {
        public static bool _Predicate_MatchesRegex(string s, string regex) {
            Regex r = new Regex(regex);
            return r.IsMatch(s);
        }

        public static Expression CallSafe(Expression target, string methodName, params Expression[] arguments) {
            var defaultTarget = Expression.Default(target.Type);
            var isNull = Expression.ReferenceEqual(target, defaultTarget);
            var argTypes = arguments.Select(a => a.Type).ToArray();
            var called = Expression.Call(target, target.Type.GetMethod(methodName, argTypes), arguments);
            var defaultCalled = Expression.Default(called.Type);
            return Expression.Condition(isNull, defaultCalled, called);
        }

        public static Expression CallAggregate(string aggregate, params Expression[] args) {
            Debug.Assert(args.Length > 0);
            List<Type> types = new List<Type>();
            types.AddRange(args.Select(e => e.Type));
            var aggregateMethod = typeof(System.Linq.Enumerable).GetMethod(aggregate, types.ToArray());
            if (aggregateMethod == null)
            {
                List<Type> typeArguments = new List<Type>();
                typeArguments.Add(ElementType(types[0]));
                types[0] = typeof(IEnumerable<>);
                for (var i = 1; i < types.Count; i++)
                {
                    if (types[i].IsGenericType)
                    {
                        foreach (var specific in types[i].GetGenericArguments()) {
                            if (!typeArguments.Contains(specific))
                            {
                                typeArguments.Add(specific);
                            }
                        }
                        types[i] = types[i].GetGenericTypeDefinition();
                    }
                }
                var aggregateOpenMethod = GetGenericMethod(typeof(System.Linq.Enumerable), aggregate, types.ToArray());
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
            // http://stackoverflow.com/questions/1121834/finding-out-if-a-type-implements-a-generic-interface

            // this conditional is necessary if myType can be an interface,
            // because an interface doesn't implement itself: for example,
            // typeof (IList<int>).GetInterfaces () does not contain IList<int>!
            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return true;
            }

            foreach (var i in type.GetInterfaces ())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return true;
                }
            }

            return false;
        }

        public static Expression CallMath(string fn, params Expression[] args) {
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
    }
}

