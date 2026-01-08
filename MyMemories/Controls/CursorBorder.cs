using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace MyMemories.Controls;

/// <summary>
/// A Grid-based control that supports cursor changes via the ProtectedCursor property.
/// Used as a splitter control with cursor support.
/// </summary>
public class SplitterBorder : Grid
{
    /// <summary>
    /// Changes the cursor for this element.
    /// </summary>
    /// <param name="cursor">The cursor to display when hovering over this element.</param>
    public void ChangeCursor(InputCursor cursor)
    {
        this.ProtectedCursor = cursor;
    }
}
