# CsvParser
Fast read data with CSV format

# Usage example

```C#
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvTest
{
    class Program
    {
        static void Main(string[] args)
        {
// Get file name our CSV file
            var input = args[0];

            using (var stream = File.OpenRead(input))
            using (var parser = new CsvParser.CsvReader(stream, Encoding.UTF8, new CsvReader.Config() { WithQuotes = true }))
            {
// Read CSV header
                if (!parser.MoveNext())
                    throw new InvalidDataException();
                var header = parser.Current.ToArray();

                while (parser.MoveNext())
                {
// Read CSV row data
                    var row = parser.Current.ToArray();
// or column data
                    var col0 = parser.Current[0];
                }
            }
        }
    }
}
```
