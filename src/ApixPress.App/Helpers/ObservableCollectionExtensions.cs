using System.Collections.ObjectModel;

namespace ApixPress.App.Helpers;

internal static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> items, IEnumerable<T> nextItems)
    {
        items.Clear();
        foreach (var item in nextItems)
        {
            items.Add(item);
        }
    }
}
