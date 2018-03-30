# CsvParser
Fast read data with CSV format according to RFC4180 with small extensions.
Best solution for parse very large data files

## Usage example

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

## Performance test

### Define data
File size 199475243 bytes, and 3337349 rows
```C#
var buffer = new MemoryStream();
using (var stream = File.OpenRead(input))
    stream.CopyTo(buffer);
```

### Simple read text data in file with encoding
```C#
using (var reader = new StreamReader(buffer, Encoding.UTF8))
{
    string line;
    while ((line = reader.ReadLine()) != null) ;
}
```

### Read and parse data from CsvReader (without read columns)
```C#
using (var parser = new CsvParser.CsvReader(buffer, Encoding.UTF8))
{
    while (parser.MoveNext()) ;
}
```

### Read and parse data from CsvReader with read first column data
```C#
using (var parser = new CsvParser.CsvReader(buffer, Encoding.UTF8))
{
    while (parser.MoveNext())
    {
        var data = parser.Current[0];
    }    
}
```

### Read and parse data from CsvReader with read all column data (10 columns)
```C#
using (var parser = new CsvParser.CsvReader(buffer, Encoding.UTF8))
{
    while (parser.MoveNext())
    {
        var data = parser.Current.ToArray();
    }    
}
```

Results of execution in seconds below in table:

|simple read|CsvReader parse|CsvReader parse and read one column|CsvReader parse and read 10 columns|
|-|-|-|-|
|0.4206415|1.1696580|1.2340430|3.0415450|

Other very popular library execute in 4.5114237 sec on the same data, but not compiled on .net standard < 2.0
