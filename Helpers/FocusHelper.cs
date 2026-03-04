using System.Windows;
using System.Windows.Media;

namespace DailyDash.Helpers
{
    public static class FocusHelper
    {
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T t)
                    return t;

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        public static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;
            
            return FindVisualParent<T>(parentObject);
        }
    }
}
