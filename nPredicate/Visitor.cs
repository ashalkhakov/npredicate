﻿namespace RealArtists.NPredicate {
  using System;
  using System.Linq;

  public interface IVisitor {
    void Visit(Predicate p);
    void Visit(Expr e);
  }

  public class Visitor : IVisitor {
    public virtual void Visit(Predicate p) { }
    public virtual void Visit(Expr e) { }
  }

  public class PascalCaseRewriter : Visitor {
    public override void Visit(Expr e) {
      if (e.ExpressionType == ExpressionType.KeyPathExpressionType) {
        var components = e.KeyPath.Split('.');
        e.KeyPath = String.Join(".", components.Select(path => PascalCase(path)));
      }
    }

    private static string PascalCase(string path) {
      if (path.Length >= 1) {
        string left = path.Substring(0, 1);
        string right = path.Substring(1);
        return left.ToUpperInvariant() + right;
      } else {
        return path;
      }
    }
  }

  public class GuidRewriter : Visitor {
    private static bool LooksLikeGuid(string str) {
      Guid ignored;
      bool parseable = Guid.TryParse(str, out ignored);
      return parseable;
    }

    public override void Visit(Expr e) {
      if (e.ExpressionType == ExpressionType.ConstantValueExpressionType && e.ConstantValue is string && LooksLikeGuid(e.ConstantValue)) {
        e.ConstantValue = Guid.Parse(e.ConstantValue);
      }
    }
  }
}
