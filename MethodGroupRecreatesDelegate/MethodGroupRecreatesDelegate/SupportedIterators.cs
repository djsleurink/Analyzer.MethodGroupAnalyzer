using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace MethodGroupRecreatesDelegate
{
    public class SupportedIterators
    {
        private static readonly Type[] _iterators = new Type[]
        {
            typeof(ForEachStatementSyntax),
            typeof(ForStatementSyntax),
            typeof(WhileStatementSyntax),
            typeof(DoStatementSyntax),
            typeof(YieldStatementSyntax),
        };

        public static Type[] Iterators => _iterators;
    }
}
