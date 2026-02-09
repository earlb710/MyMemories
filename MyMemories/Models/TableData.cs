using System.Collections.Generic;
using System.Linq;

namespace MyMemories.Models;

/// <summary>
/// Represents extracted table data from a PDF document.
/// </summary>
public class TableData
{
    /// <summary>
    /// Gets or sets the page number where the table was found (1-indexed).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the rows of the table, where each row is a list of cell values.
    /// </summary>
    public List<List<string>> Rows { get; set; } = new();

    /// <summary>
    /// Gets or sets the template ID this table was matched to (if using template-based extraction).
    /// </summary>
    public int? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets whether the first row is a header row.
    /// </summary>
    public bool HasHeaderRow { get; set; }

    /// <summary>
    /// Gets the header row if HasHeaderRow is true, otherwise null.
    /// </summary>
    public List<string>? HeaderRow => HasHeaderRow && Rows?.Count > 0 ? Rows[0] : null;

    /// <summary>
    /// Gets the number of rows in the table.
    /// </summary>
    public int RowCount => Rows?.Count ?? 0;

    /// <summary>
    /// Gets the number of columns in the table (based on the first row).
    /// </summary>
    public int ColumnCount => Rows?.FirstOrDefault()?.Count ?? 0;
}
