﻿using System;
using System.Collections.Generic;
using Xunit;

namespace RealArtists.NPredicate.Tests;

public class ParseTest
{
    [Fact]
    public void Test()
    {
        string predicateFormat = "1 = 2";
        PredicateParser parser = new(predicateFormat);
        var predicate = parser.ParsePredicate();
        Assert.NotNull(predicate);
    }

    [Fact]
    public void TestComparisonOptions()
    {
        string predicateFormat = "'Hello World' =[cd] 'hello world'";
        PredicateParser parser = new(predicateFormat);
        var predicate = parser.ParsePredicate();
        Assert.NotNull(predicate);
    }

    [Fact]
    public void TestFormatArguments()
    {
        var predicate = Predicate.Parse("%d = 1", 10);
        Assert.Equal("(10 == 1)", predicate.Format);
    }

    class Prop
    {
        public string A { get; set; }
        public Prop P { get; set; }
    }

    [Fact]
    public void TestKeyPathFormatArguments()
    {
        var predicate = Predicate.Parse("%K =[c] 'Hi'", "A");
        Console.WriteLine(predicate.Format);
        var prop = new Prop() { A = "hi" };
        Assert.True(predicate.EvaluateObject(prop));
    }

    [Fact]
    public void TestSelfBeginsWith()
    {
        var predicate = Predicate.Parse("SELF BEGINSWITH 'N'");
        string[] array = ["James", "Jack", "June", "John", "Jason", "Jill", "Nick"];
        Assert.Single(array.Where(predicate));
    }

    [Fact]
    public void TestArithmetic()
    {
        var expr = Expr.Parse("1 + 2 + 3 * 9");
        var result = expr.Value<int>();
        Assert.Equal(30, result);
    }

    [Fact]
    public void TestKeyPath()
    {
        var predicate = Predicate.Parse("A MATCHES '.*World$'");
        var prop = new Prop() { A = "Hello World" };
        Assert.True(predicate.EvaluateObject(prop));
    }

    [Fact]
    public void TestArrayIndex()
    {
        var array = new int[] { 0, 1, 1, 2, 3, 5, 8, 13, 21 };
        var first = Expr.Parse("%@[FIRST]", array).Value<int>();
        Assert.Equal(array[0], first);
        var last = Expr.Parse("%@[LAST]", array).Value<int>();
        Assert.Equal(array[^1], last);
        var size = Expr.Parse("%@[SIZE]", array).Value<int>();
        Assert.Equal(array.Length, size);
    }

    [Fact]
    public void TestEnumerableIndex()
    {
        var array = new int[] { 0, 1, 1, 2, 3, 5, 8, 13, 21 };
        var list = new System.Collections.Generic.List<int>(array);
        var first = Expr.Parse("%@[FIRST]", list).Value<int>();
        Assert.Equal(array[0], first);
        var last = Expr.Parse("%@[LAST]", list).Value<int>();
        Assert.Equal(array[^1], last);
        var size = Expr.Parse("%@[SIZE]", list).Value<int>();
        Assert.Equal(array.Length, size);
    }

    [Fact]
    public void TestNestedKeyPath()
    {
        var predicate = Predicate.Parse("P.P.A MATCHES '.*World$'");
        var p = new Prop() { P = new Prop() { P = new Prop() { A = "Hello World" } } };
        Assert.True(predicate.EvaluateObject(p));
    }

    public class Document
    {
        public User Author { get; set; }
        public string Content { get; set; }
        public string[] Keywords { get; set; }

        public class User
        {
            public string Name { get; set; }
        }
    }

    [Fact]
    public void TestSubquery()
    {
        // SUBQUERY(keywords, $k, $k BEGINSWITH 'hello').@count
        var count = Expr.Parse("SUBQUERY(keywords, $k, $k BEGINSWITH 'hello').@count");

        var doc = new Document
        {
            Keywords = ["hello world", "hello vietnam", "hello usa", "goodbye cruel world"]
        };

        var helloCount = count.ValueWithObject<Document, int>(doc);
        Assert.Equal(3, helloCount);
    }

    [Fact]
    public void TestSubquery2()
    {
        // SUBQUERY(keywords, $k, $k BEGINSWITH 'hello').@count
        var count = Expr.Parse("subquery(keywords, $k, $k BEGINSWITH 'hello')[SIZE]");

        var doc = new Document
        {
            Keywords = ["hello world", "hello vietnam", "hello usa", "goodbye cruel world"]
        };

        var helloCount = count.ValueWithObject<Document, int>(doc);
        Assert.Equal(3, helloCount);
    }

    [Fact]
    public void TestNoArgsFunction()
    {
        var now = Expr.Parse("NOW()").Value<DateTime>();
        Assert.True(Math.Abs(DateTime.UtcNow.Subtract(now).TotalSeconds) <= 1);

        var r1 = Expr.Parse("RANDOM()").Value<int>();
        var r2 = Expr.Parse("RANDOM()").Value<int>();
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void Test1ArgFunction()
    {
        var sum = Expr.Parse("SUM(%@)", new[] { 5, 8 }).Value<int>();
        Assert.Equal(13, sum);
    }

    [Fact]
    public void Test2ArgFunction()
    {
        var mod = Expr.Parse("FUNCTION('modulus:by:', 10, 7)").Value<int>();
        Assert.Equal(10 % 7, mod);
    }

    [Fact]
    public void TestEmptyAggregate()
    {
        Assert.Equal(0, Expr.Parse("{}.@count").Value<int>());
    }

    [Fact]
    public void TestAggregate()
    {
        Assert.Equal(3, Expr.Parse("{0,1,2}.@count").Value<int>());
    }

    [Fact]
    public void TestValueFalse()
    {
        Assert.False(Expr.Parse("NO").Value<bool>());
    }

    [Fact]
    public void TestValueTrue()
    {
        Assert.True(Expr.Parse("YES").Value<bool>());
    }

    [Fact]
    public void TestNull()
    {
        Assert.True(Predicate.Parse("SELF.A == nil").EvaluateObject(new Prop()));
    }

    [Fact]
    public void TestTruePredicate()
    {
        Assert.True(Predicate.Parse("TRUEPREDICATE").EvaluateObject<object>(null));
    }

    [Fact]
    public void TestFalsePredicate()
    {
        Assert.False(Predicate.Parse("FALSEPREDICATE").EvaluateObject<object>(null));
    }

    [Fact]
    public void TestOrPredicate()
    {
        Assert.True(Predicate.Parse("TRUEPREDICATE OR TRUEPREDICATE").EvaluateObject<object>(null));
        Assert.True(Predicate.Parse("FALSEPREDICATE OR TRUEPREDICATE").EvaluateObject<object>(null));
        Assert.True(Predicate.Parse("TRUEPREDICATE OR FALSEPREDICATE").EvaluateObject<object>(null));
        Assert.False(Predicate.Parse("FALSEPREDICATE OR FALSEPREDICATE").EvaluateObject<object>(null));
    }

    [Fact]
    public void TestAndPredicate()
    {
        Assert.True(Predicate.Parse("TRUEPREDICATE AND TRUEPREDICATE").EvaluateObject<object>(null));
        Assert.False(Predicate.Parse("FALSEPREDICATE AND TRUEPREDICATE").EvaluateObject<object>(null));
        Assert.False(Predicate.Parse("TRUEPREDICATE AND FALSEPREDICATE").EvaluateObject<object>(null));
        Assert.False(Predicate.Parse("FALSEPREDICATE AND FALSEPREDICATE").EvaluateObject<object>(null));
    }

    [Fact]
    public void TestNotPredicate()
    {
        Assert.True(Predicate.Parse("NOT FALSEPREDICATE").EvaluateObject<object>(null));
        Assert.False(Predicate.Parse("NOT TRUEPREDICATE").EvaluateObject<object>(null));
    }

    [Fact]
    public void TestBetween()
    {
        Assert.True(Predicate.Parse("%d BETWEEN { 0, 2 }", 1).EvaluateObject<object>(null));
        Assert.False(Predicate.Parse("%d BETWEEN { 0, 2 }", 3).EvaluateObject<object>(null));
    }

    [Fact]
    public void TestPower()
    {
        Assert.Equal(8.0, Expr.Parse("2.0**3.0").Value<double>());
    }

    [Fact]
    public void TestSub()
    {
        Assert.Equal(1, Expr.Parse("3-2").Value<int>());
    }

    [Fact]
    public void TestDiv()
    {
        Assert.Equal(10, Expr.Parse("100/10").Value<int>());
    }

    [Fact]
    public void TestUnaryMinus()
    {
        Assert.Equal(-3, Expr.Parse("-(1+2)").Value<int>());
    }

    [Fact]
    public void TestNegativeNumber()
    {
        Assert.Equal(-1.0, Expr.Parse("-1.0").Value<double>());
    }

    [Fact]
    public void TestVariableAssignment()
    {
        Assert.Equal(3, Expr.Parse("$a := 1+2").Value<int>());
    }

    class C
    {
        public string S { get; set; }
    }

    class B
    {
        public IEnumerable<C> Collection { get; set; }
    }

    class A
    {
        public B B { get; set; }
    }

    [Fact]
    public void TestKeyPathCollection()
    {
        C c0 = new() { S = "Hello World" };
        C c1 = new() { S = "Goodbye Cruel World" };

        B b = new() { Collection = [c0, c1] };

        A a = new() { B = b };


        var any = Predicate.Parse("ANY B.Collection.S BEGINSWITH 'Hello'");
        var all = Predicate.Parse("ALL B.Collection.S ENDSWITH 'World'");
        var allFail = Predicate.Parse("ALL B.Collection.S BEGINSWITH 'Hello'");
        var none = Predicate.Parse("NONE B.Collection.S BEGINSWITH 'Gday'");
        var noneFail = Predicate.Parse("NONE B.Collection.S BEGINSWITH 'Hello'");

        Assert.True(any.EvaluateObject(a));
        Assert.True(all.EvaluateObject(a));
        Assert.False(allFail.EvaluateObject(a));
        Assert.True(none.EvaluateObject(a));
        Assert.False(noneFail.EvaluateObject(a));
    }

    [Fact]
    public void TestCaseInsensitiveProperties()
    {
        C c0 = new() { S = "Hello World" };
        C c1 = new() { S = "Goodbye Cruel World" };

        B b = new() { Collection = [c0, c1] };

        A a = new() { B = b };

        var any = Predicate.Parse("ANY b.collection.s BEGINSWITH 'Hello'");
        Assert.True(any.EvaluateObject(a));
    }

    [Fact]
    public void TestCastStringToNumber()
    {
        var e = Expr.Parse("CAST('123.0', 'NSNumber')");
        Assert.Equal(123.0, e.Value<double>());
    }

    [Fact]
    public void TestCastDate()
    {
        var e = Expr.Parse("CAST(CAST(now(), 'NSNumber'), 'NSDate')");
        DateTime dt = e.Value<DateTime>();
        Assert.True((DateTime.UtcNow - dt).TotalSeconds < 1.0);
    }

    [Fact]
    public void TestDateArithmetic()
    {
        var e = Expr.Parse("FUNCTION(now(), 'dateByAddingDays:', -2)");
        DateTime dt = e.Value<DateTime>();
        Assert.True(Math.Abs((DateTime.UtcNow.AddDays(-2) - dt).TotalSeconds) < 1.0);
    }

    [Fact]
    public void TestVariableBindings()
    {
        var pred = Predicate.Parse("$varA == $varB");
        pred.VariableBindings = new Dictionary<string, dynamic>() { { "varA", 42 }, { "varB", 42 } };
        Assert.True(pred.EvaluateObject<object>(null));
        pred.VariableBindings = new Dictionary<string, dynamic>() { { "varA", 42 }, { "varB", 43 } };
        Assert.False(pred.EvaluateObject<object>(null));
    }

    [Fact]
    public void TestCompareStringAndGuid()
    {
        var guid = Guid.NewGuid();
        var pred = Predicate.Parse("%@ == %@", guid.ToString(), guid);
        Assert.True(pred.EvaluateObject<object>(null));
    }

    [Fact]
    public void TestStringEscape()
    {
        Assert.Equal("Hello 'World'", Expr.Parse("'Hello \\'World\\''").Value<string>());
    }

    [Fact]
    public void TestUnicodeEscape()
    {
        Assert.Equal("☂", "\u2602");
        Assert.Equal("☂", Expr.Parse("'\\u2602'").Value<string>());
    }

    [Fact]
    public void TestNumericUpcast()
    {
        var pred = Predicate.Parse("%d == %ld", 42, 42L);
        Assert.True(pred.EvaluateObject<object>(null));
    }

    [Fact]
    public void TestFloatConversion()
    {
        var pred = Predicate.Parse("10 < 11.0");
        Assert.True(pred.EvaluateObject<object>(null));
    }

    [Fact]
    public void TestStringToGuidUpcast()
    {
        var guid = Guid.NewGuid();
        var guidStr = guid.ToString();
        var pred1 = Predicate.Parse("%@ == %@", guid, guidStr);
        var pred2 = Predicate.Parse("%@ == %@", guid, guidStr);

        Assert.True(pred1.EvaluateObject<object>(null));
        Assert.True(pred2.EvaluateObject<object>(null));
    }
}

