using System.Globalization;
using System.Numerics;
using System.Reflection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace DMTQ_Tools.Components.Shared;

internal static class GridItemsProviderFactory
{
    public static GridItemsProvider<TItem> Create<TItem>(IReadOnlyList<TItem> items)
        => request =>
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            IEnumerable<TItem> sortedItems;
            var sortProperties = request.GetSortByProperties();
            if (sortProperties.Count == 1
                && TryGetNumericIdentifierSort(items, sortProperties.First(), out var numericSort))
            {
                sortedItems = numericSort;
            }
            else
            {
                sortedItems = request.ApplySorting(items.AsQueryable());
            }

            var page = sortedItems.Skip(request.StartIndex);
            if (request.Count is { } count)
                page = page.Take(count);

            return ValueTask.FromResult(GridItemsProviderResult.From(page.ToArray(), items.Count));
        };

    private static bool TryGetNumericIdentifierSort<TItem>(
        IReadOnlyList<TItem> items,
        SortedProperty sortProperty,
        out IEnumerable<TItem> sortedItems)
    {
        sortedItems = items;
        var propertyName = sortProperty.PropertyName;
        if (string.IsNullOrWhiteSpace(propertyName)
            || !(propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)
                || propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)))
            return false;

        var property = typeof(TItem).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType != typeof(string))
            return false;

        var identifiers = new string?[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            identifiers[index] = property.GetValue(items[index]) as string;
            if (!BigInteger.TryParse(identifiers[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return false;
        }

        var comparer = sortProperty.Direction == DataGridSortDirection.Descending
            ? NumericIdentifierComparer.Descending
            : NumericIdentifierComparer.Ascending;
        sortedItems = items.OrderBy(item => property.GetValue(item) as string, comparer);
        return true;
    }

    private sealed class NumericIdentifierComparer : IComparer<string?>
    {
        public static NumericIdentifierComparer Ascending { get; } = new(descending: false);
        public static NumericIdentifierComparer Descending { get; } = new(descending: true);

        private readonly bool _descending;

        private NumericIdentifierComparer(bool descending) => _descending = descending;

        public int Compare(string? leftText, string? rightText)
        {
            BigInteger.TryParse(leftText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left);
            BigInteger.TryParse(rightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var right);
            var comparison = left.CompareTo(right);
            return _descending ? -comparison : comparison;
        }
    }
}

public static class TableItemOrdering
{
    public static IOrderedEnumerable<TItem> ByIdentifierDescending<TItem>(
        this IEnumerable<TItem> items,
        Func<TItem, string?> identifierSelector)
        => items.OrderBy(item => identifierSelector(item), DescendingIdentifierComparer.Instance);

    private sealed class DescendingIdentifierComparer : IComparer<string?>
    {
        public static DescendingIdentifierComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return 1;
            if (y is null) return -1;

            if (long.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left)
                && long.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var right))
                return right.CompareTo(left);

            return StringComparer.OrdinalIgnoreCase.Compare(y, x);
        }
    }
}
