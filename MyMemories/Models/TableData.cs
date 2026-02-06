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
    /// Gets the number of rows in the table.
    /// </summary>
    public int RowCount => Rows?.Count ?? 0;

    /// <summary>
    /// Gets the number of columns in the table (based on the first row).
    /// </summary>
    public int ColumnCount => Rows?.FirstOrDefault()?.Count ?? 0;
}
