using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck {

    // Helper to create .iqy files for consumption by Excel
    // An IQY will be generated for each cache entry so Excel
    // users can add cache contents to their sheets
    class ExcelQueryHelper {
        private string[] iqy_lines = {
            "WEB",
            "1",        // table index
            "<url>",    // eg http://localhost:9271/excel/quandl/yield.csv
            "",
            "Selection=1",
            "Formatting=None",
            "PreFormattedTextToColumns=True",
            "ConsecutiveDelimitersAsOne=True",
            "SingleBlockTextImport=False",
            "DisableDateRecognition=False",
            "DisableRedirections=False" };

        public string[] Lines { get => iqy_lines; }

        public ExcelQueryHelper(string url) {
            iqy_lines[2] = url;
        }
    }


}
