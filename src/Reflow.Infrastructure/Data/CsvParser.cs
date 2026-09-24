namespace Reflow.Infrastructure.Data;

public static class CsvParser
{
    public static Dataset Parse(string csvText, char delimiter, bool hasHeader)
    {
        var rows = SplitRows(csvText, delimiter);

        if (rows.Count > Dataset.MaxRows + 1)
            throw new InvalidOperationException($"CSV has too many rows (max {Dataset.MaxRows})");

        var dataset = new Dataset();

        if (rows.Count == 0)
            return dataset;

        var startIndex = 0;
        if (hasHeader)
        {
            dataset.Columns.AddRange(rows[0].Select(h => h ?? string.Empty));
            startIndex = 1;
        }
        else
        {
            var width = rows.Max(r => r.Count);
            for (var i = 0; i < width; i++)
                dataset.Columns.Add($"col{i + 1}");
        }

        if (dataset.Columns.Count == 0)
            throw new InvalidOperationException("CSV has no columns");

        if (dataset.Columns.Count > 500)
            throw new InvalidOperationException("CSV has too many columns (max 500)");

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in dataset.Columns)
        {
            if (!seenNames.Add(col))
                throw new InvalidOperationException($"CSV header has duplicate column name: '{col}'");
        }

        for (var i = startIndex; i < rows.Count; i++)
        {
            var cells = rows[i];
            if (cells.Count == 1 && cells[0] is null)
                continue;

            var row = new List<string?>();
            for (var c = 0; c < dataset.Columns.Count; c++)
                row.Add(c < cells.Count ? cells[c] : null);
            dataset.Rows.Add(row);
        }

        dataset.Quality.InputCount = dataset.Rows.Count;
        dataset.Quality.OutputCount = dataset.Rows.Count;
        return dataset;
    }

    private static List<List<string?>> SplitRows(string text, char delimiter)
    {
        var rows = new List<List<string?>>();
        var currentRow = new List<string?>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var fieldHasContent = false;
        var i = 0;

        void EndField()
        {
            currentRow.Add(fieldHasContent || field.Length > 0 ? field.ToString() : null);
            field.Clear();
            fieldHasContent = false;
        }

        while (i < text.Length)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        fieldHasContent = true;
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    field.Append(ch);
                    fieldHasContent = true;
                    i++;
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
                i++;
            }
            else if (ch == delimiter)
            {
                EndField();
                i++;
            }
            else if (ch == '\r' || ch == '\n')
            {
                EndField();
                rows.Add(currentRow);
                currentRow = new List<string?>();
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i += 2;
                else
                    i++;
            }
            else
            {
                field.Append(ch);
                fieldHasContent = true;
                i++;
            }
        }

        if (inQuotes)
            throw new InvalidOperationException("Malformed CSV: unterminated quoted field");

        if (fieldHasContent || field.Length > 0 || currentRow.Count > 0)
        {
            EndField();
            rows.Add(currentRow);
        }

        return rows.Where(r => !(r.Count == 1 && r[0] is null)).ToList();
    }
}
