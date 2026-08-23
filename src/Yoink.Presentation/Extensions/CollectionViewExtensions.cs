using System.ComponentModel;

namespace Yoink.Presentation.Extensions;

/// <summary>
/// Provides safe refresh methods for WPF <see cref="ICollectionView"/> instances,
/// preventing crashes when a DataGrid or item view is in an active AddNew or EditItem transaction.
/// </summary>
public static class CollectionViewExtensions
{
    public static void SafeRefresh(this ICollectionView? view)
    {
        if (view is null)
        {
            return;
        }

        try
        {
            if (view is IEditableCollectionView editable)
            {
                if (editable.IsAddingNew)
                {
                    try { editable.CommitNew(); } catch { editable.CancelNew(); }
                }

                if (editable.IsEditingItem)
                {
                    try { editable.CommitEdit(); } catch { editable.CancelEdit(); }
                }
            }

            view.Refresh();
        }
        catch (InvalidOperationException)
        {
            // Transaction in progress or re-entrant refresh call — safely ignore to prevent crashing the UI.
        }
    }
}
