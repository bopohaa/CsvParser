# CsvParser
Fast read data with CSV format according to RFC4180 with small extensions.
Best solution for parse very large data files

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
            using (var parser = new CsvParser.CsvReader(stream, Encoding.UTF8, 
// Include quotes (if exists) in result
// default WithQuotes = false, i.e. column value "'test'" translated to 'test'
                new CsvParser.CsvReader.Config() { WithQuotes = true }))
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
