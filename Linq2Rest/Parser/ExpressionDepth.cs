namespace LinqConvertTools.Parser
{
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Measures whether an expression tree nests deeper than a limit. The walk stops descending once the limit
    /// is passed, so its own recursion never goes deeper than the limit, however deep the tree is. A limit too
    /// large for the stack raises <see cref="InsufficientExecutionStackException"/> instead of overflowing it.
    /// A reducible extension node is measured as the node it reduces to, so the depth is that of the tree once
    /// reduced, and subtrees an extension node only keeps for building other nodes are not walked.
    /// </summary>
    internal sealed class ExpressionDepth : ExpressionVisitor
    {
        private readonly int _maxDepth;
        private int _depth;
        private bool _exceeded;

        private ExpressionDepth(int maxDepth)
        {
            _maxDepth = maxDepth;
        }

        public static bool Exceeds(Expression expression, int maxDepth)
        {
            var measure = new ExpressionDepth(maxDepth);
            measure.Visit(expression);
            return measure._exceeded;
        }

        public override Expression? Visit(Expression? node)
        {
            if (node is null || _exceeded)
            {
                return node;
            }

            while (node.NodeType == ExpressionType.Extension && node.CanReduce)
            {
                node = node.Reduce();
            }

            if (++_depth > _maxDepth)
            {
                _exceeded = true;
            }
            else
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                base.Visit(node);
            }

            _depth--;
            return node;
        }
    }
}
