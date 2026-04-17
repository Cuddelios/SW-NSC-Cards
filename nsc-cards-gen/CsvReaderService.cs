using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace SvgPdfGenerator;

public sealed class CsvReaderService
{
    public List<Dictionary<string, string>> Read(string csvPath, char delimiter)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException("CSV path must not be empty.", nameof(csvPath));
        }

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("CSV file was not found.", csvPath);
        }

        using var reader = new StreamReader(csvPath);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
        {
            return new List<Dictionary<string, string>>();
        }

        csv.ReadHeader();

        string[] headers = csv.HeaderRecord
            ?? throw new InvalidOperationException("CSV header could not be read.");

        var result = new List<Dictionary<string, string>>();

        while (csv.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            result.Add(row);
        }

        return result;
    }
}