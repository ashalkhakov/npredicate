﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TestSupport.EfHelpers;
using Xunit;

namespace RealArtists.NPredicate.Tests;


public class TestEFUser
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class TestEFDocument
{
    public TestEFDocument()
    {
        Watchers = [];
        CreationDate = DateTime.UtcNow;
        ModificationDate = DateTime.UtcNow;
    }

    public int Id { get; set; }
    public string Content { get; set; }
    public virtual ICollection<TestEFUser> Watchers { get; set; }
    public TestEFUser Author { get; set; }
    public DateTime ModificationDate { get; set; }
    public DateTime CreationDate { get; set; }

    public DateTime? MaybeDate { get; set; }
}

public class TestEFContext(DbContextOptions<TestEFContext> options) : DbContext(options)
{
    public virtual DbSet<TestEFDocument> Documents { get; set; }
    public virtual DbSet<TestEFUser> Users { get; set; }
}

public class SqliteFixture : IDisposable
{
    DbContextOptionsDisposable<TestEFContext> _options;

    public SqliteFixture()
    {
        _options = SqliteInMemory.CreateOptions<TestEFContext>();
        _options.TurnOffDispose();

        using var ctx = new TestEFContext(_options);

        ctx.Database.EnsureCreated();

        var james = new TestEFUser() { Name = "James Howard" };
        ctx.Users.Add(james);
        var nick = new TestEFUser() { Name = "Nick Sivo" };
        ctx.Users.Add(nick);

        var doc1 = new TestEFDocument() { Content = "Hello World" };
        ctx.Documents.Add(doc1);
        doc1.Watchers.Add(james);
        doc1.Watchers.Add(nick);
        doc1.CreationDate = DateTime.UtcNow.AddDays(-3);
        doc1.ModificationDate = DateTime.UtcNow.AddDays(-2);
        doc1.MaybeDate = DateTime.UtcNow.AddDays(-1);

        ctx.Documents.Add(new TestEFDocument() { Content = "Goodbye Cruel World" });

        ctx.SaveChanges();
    }

    public TestEFContext CreateContext()
    {
        return new TestEFContext(_options);
    }

    public void Dispose()
    {
        if (_options != null)
        {
            _options.Dispose();
            _options = null;
        }
    }
}

public class EFTest(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture = fixture;

    [Fact]
    public void TestEFSanity()
    {
        using var ctx = _fixture.CreateContext();
        
        Assert.True(ctx.Documents.Where(x => x.Content == "Hello World").Any());
        Assert.False(ctx.Documents.Where(x => x.Content == "Nobody here but us chickens").Any());
    }

    [Fact]
    public void TestStringEquals()
    {
        using var ctx = _fixture.CreateContext();

        var needle = Expr.MakeConstant("Hello World");
        var content = Expr.MakeKeyPath("Content");

        var pred = ComparisonPredicate.EqualTo(content, needle);
        var matches = ctx.Documents.Where(pred);

        Assert.Equal(1, matches.Count());
    }

    [Fact]
    public void TestCaseInsensitive()
    {
        // Content ==[c] "hello world"

        using var ctx = _fixture.CreateContext();
        
        var needle = Expr.MakeConstant("hello world");
        var content = Expr.MakeKeyPath("Content");

        var pred = ComparisonPredicate.EqualTo(content, needle, ComparisonPredicateModifier.Direct, ComparisonPredicateOptions.CaseInsensitive);
        var matches = ctx.Documents.Where(pred);
        Assert.Single(matches);

        var pred2 = ComparisonPredicate.EqualTo(content, needle, ComparisonPredicateModifier.Direct);
        var matches2 = ctx.Documents.Where(pred2);
        Assert.Empty(matches2);

    }

    [Fact]
    public void TestSubquery()
    {
        // COUNT(SUBQUERY(Watchers, $user, $user.Name BEGINSWITH "James")) > 0
        // (x => x.Watchers.Where(user => user.Name.StartsWith("James")).Count() > 0);

        using var ctx = _fixture.CreateContext();

        var collection = Expr.MakeKeyPath("Watchers");
        var needle = Expr.MakeConstant("James");
        var user = Expr.MakeVariable("$user");
        var name = Expr.MakeKeyPath(user, "Name");
        var subquery = Expr.MakeSubquery(collection, "$user", ComparisonPredicate.BeginsWith(name, needle));

        var count = Expr.MakeFunction("count:", subquery);

        var pred = ComparisonPredicate.GreaterThan(count, Expr.MakeConstant(0));

        var matches = ctx.Documents.Where(pred);

        Assert.Equal(1, matches.Count());
    }

    [Fact]
    public void TestSubquery2()
    {
        // SUBQUERY(Watchers, $user, $user.Name BEGINSWITH "James").@count > 0
        // (x => x.Watchers.Where(user => user.Name.StartsWith("James")).Count() > 0);

        using var ctx = _fixture.CreateContext();

        var collection = Expr.MakeKeyPath("Watchers");
        var needle = Expr.MakeConstant("James");
        var user = Expr.MakeVariable("$user");
        var name = Expr.MakeKeyPath(user, "Name");
        var subquery = Expr.MakeSubquery(collection, "$user", ComparisonPredicate.BeginsWith(name, needle));

        var count = Expr.MakeKeyPath(subquery, "@count");

        var pred = ComparisonPredicate.GreaterThan(count, Expr.MakeConstant(0));

        var matches = ctx.Documents.Where(pred);

        Assert.Equal(1, matches.Count());
    }

    [Fact]
    public void TestSubquery3()
    {
        using var ctx = _fixture.CreateContext();

        var pred = Predicate.Parse("SUBQUERY(Watchers, $user, $user.Name BEGINSWITH 'James').@count > 0");
        var matches = ctx.Documents.Where(pred);

        Assert.Equal(1, matches.Count());
    }

    [Fact]
    public void TestMatches()
    {
        using var ctx = _fixture.CreateContext();

        var predicate = Predicate.Parse("Content MATCHES '.*World$'");
        var d2 = ctx.Documents.Where(predicate);
        Assert.Equal(2, d2.Count());
        Assert.All(d2, (d) => Assert.Matches(".*World$", d.Content));
    }

    [Fact]
    public void TestName()
    {
        using var ctx = _fixture.CreateContext();

        var predicate = Predicate.Parse("Author.Name == 'James Howard'");
        Assert.False(ctx.Documents.Where(predicate).Any());
    }

    [Fact]
    public void TestDateComparison()
    {
        using var ctx = _fixture.CreateContext();

        var nope = Predicate.Parse("CreationDate > ModificationDate");
        var yep = Predicate.Parse("CreationDate < ModificationDate");

        Assert.False(ctx.Documents.Where(nope).Any());
        Assert.True(ctx.Documents.Where(yep).Any());
    }

    [Fact]
    public void TestDateArithmetic()
    {
        using var ctx = _fixture.CreateContext();

        var nope = Predicate.Parse("CreationDate < FUNCTION(now(), 'dateByAddingDays:', -4)");
        var yep = Predicate.Parse("CreationDate < FUNCTION(now(), 'dateByAddingDays:', -2)");

        Assert.False(ctx.Documents.Where(nope).Any());
        Assert.True(ctx.Documents.Where(yep).Any());
    }

    [Fact]
    public void TestNullableDateComparison()
    {
        using var ctx = _fixture.CreateContext();

        var yep = Predicate.Parse("MaybeDate < NOW()");
        Assert.True(ctx.Documents.Where(yep).Any());
    }

    [Fact]
    public void TestNullableDateAsNull()
    {
        using var ctx = _fixture.CreateContext();

        var yep = Predicate.Parse("MaybeDate == nil");
        Assert.True(ctx.Documents.Where(yep).Any());
    }
}
