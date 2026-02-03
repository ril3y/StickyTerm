using System.Windows;
using System.Windows.Controls;

namespace StickyTerm.Behaviors;

/// <summary>
/// Attached behavior to enable auto-scroll on TextBox and RichTextBox controls.
/// </summary>
public static class AutoScrollBehavior
{
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScroll(DependencyObject obj)
    {
        return (bool)obj.GetValue(AutoScrollProperty);
    }

    public static void SetAutoScroll(DependencyObject obj, bool value)
    {
        obj.SetValue(AutoScrollProperty, value);
    }

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.TextChanged += TextBox_TextChanged;
            }
            else
            {
                textBox.TextChanged -= TextBox_TextChanged;
            }
        }
        else if (d is RichTextBox richTextBox)
        {
            if ((bool)e.NewValue)
            {
                richTextBox.TextChanged += RichTextBox_TextChanged;
            }
            else
            {
                richTextBox.TextChanged -= RichTextBox_TextChanged;
            }
        }
        else if (d is ScrollViewer scrollViewer)
        {
            // For ScrollViewer, we need to watch for content changes
            if ((bool)e.NewValue)
            {
                scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
            else
            {
                scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            }
        }
    }

    private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.ScrollToEnd();
        }
    }

    private static void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is RichTextBox richTextBox)
        {
            richTextBox.ScrollToEnd();
        }
    }

    private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && e.ExtentHeightChange > 0)
        {
            scrollViewer.ScrollToEnd();
        }
    }
}
