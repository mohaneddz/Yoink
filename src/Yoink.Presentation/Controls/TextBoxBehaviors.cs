using System.Windows;
using System.Windows.Controls;
using Yoink.Shared.Validation;

namespace Yoink.Presentation.Controls;

/// <summary>Attached behaviors for TextBox controls.</summary>
public static class TextBoxBehaviors
{
    /// <summary>
    /// When true, intercepts paste events (Ctrl+V, context menu) to ensure pasted multiline text
    /// is truncated to a single line and whitespace-trimmed.
    /// </summary>
    public static readonly DependencyProperty SingleLinePasteProperty =
        DependencyProperty.RegisterAttached(
            "SingleLinePaste",
            typeof(bool),
            typeof(TextBoxBehaviors),
            new PropertyMetadata(false, OnSingleLinePasteChanged));

    public static bool GetSingleLinePaste(DependencyObject obj) => (bool)obj.GetValue(SingleLinePasteProperty);
    public static void SetSingleLinePaste(DependencyObject obj, bool value) => obj.SetValue(SingleLinePasteProperty, value);

    private static void OnSingleLinePasteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            DataObject.AddPastingHandler(textBox, OnTextBoxPaste);
        }
        else
        {
            DataObject.RemovePastingHandler(textBox, OnTextBoxPaste);
        }
    }

    private static void OnTextBoxPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.AcceptsReturn)
        {
            return;
        }

        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText, true))
        {
            var raw = e.DataObject.GetData(DataFormats.UnicodeText, true) as string;
            if (raw is not null)
            {
                var clean = TextSanitizer.TruncateToSingleLine(raw);
                var dataObject = new DataObject(DataFormats.UnicodeText, clean);
                e.DataObject = dataObject;
            }
        }
        else if (e.DataObject.GetDataPresent(DataFormats.Text, true))
        {
            var raw = e.DataObject.GetData(DataFormats.Text, true) as string;
            if (raw is not null)
            {
                var clean = TextSanitizer.TruncateToSingleLine(raw);
                var dataObject = new DataObject(DataFormats.Text, clean);
                e.DataObject = dataObject;
            }
        }
    }
}
