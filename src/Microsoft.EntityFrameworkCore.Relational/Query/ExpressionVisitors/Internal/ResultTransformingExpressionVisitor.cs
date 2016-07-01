// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Extensions.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class ResultTransformingExpressionVisitor<TResult> : ExpressionVisitorBase
    {
        private readonly RelationalQueryCompilationContext _relationalQueryCompilationContext;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public ResultTransformingExpressionVisitor(
            [NotNull] IQuerySource outerQuerySource,
            [NotNull] RelationalQueryCompilationContext relationalQueryCompilationContext)
        {
            Check.NotNull(outerQuerySource, nameof(outerQuerySource));
            Check.NotNull(relationalQueryCompilationContext, nameof(relationalQueryCompilationContext));

            _relationalQueryCompilationContext = relationalQueryCompilationContext;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Check.NotNull(node, nameof(node));

            if (node.Method.MethodIsClosedFormOf(
                _relationalQueryCompilationContext.QueryMethodProvider.ShapedQueryMethod))
            {
                var queryArguments = node.Arguments.ToList();

                queryArguments[2] = Expression.Default(typeof(int?));

                return ResultOperatorHandler
                    .CallWithPossibleCancellationToken(
                        _relationalQueryCompilationContext.QueryMethodProvider
                            .GetResultMethod.MakeGenericMethod(typeof(TResult)),
                        Expression.Call(
                            _relationalQueryCompilationContext.QueryMethodProvider.QueryMethod,
                            queryArguments));
            }

            if (node.Method.MethodIsClosedFormOf(
                _relationalQueryCompilationContext.QueryMethodProvider.PreExecuteMethod))
            {
                var sourceArgument = Visit(node.Arguments[1]);
                var sourceArgumentMethod = sourceArgument as MethodCallExpression;
                if (sourceArgumentMethod != null && sourceArgumentMethod.Method.MethodIsClosedFormOf(
                    _relationalQueryCompilationContext.QueryMethodProvider.GetResultMethod))
                {
                    var getResultArgument = sourceArgumentMethod.Arguments[0];
                    var newGetResultArgument = Expression.Call(
                        _relationalQueryCompilationContext.QueryMethodProvider.PreExecuteMethod.MakeGenericMethod(typeof(ValueBuffer)),
                        node.Arguments[0], getResultArgument, node.Arguments[2]);

                    return Expression.Call(sourceArgumentMethod.Method, newGetResultArgument);
                }

                return sourceArgumentMethod;
            }

            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var expression in node.Arguments)
            {
                var newExpression = Visit(expression);

                if (newExpression != expression)
                {
                    return newExpression;
                }
            }

            return node;
        }
    }
}
