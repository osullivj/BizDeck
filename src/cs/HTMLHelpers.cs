using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck {

    // Pre encode strings as byte[] to minimise work done streaming
    // HTML tables back to Excel.
    public class HTMLHelpers {
        public static byte[] TableStart = Encoding.ASCII.GetBytes("<table border=\"1\">");
        public static byte[] TableEnd = Encoding.ASCII.GetBytes("</table>");
        public static byte[] HeaderFieldStart = Encoding.ASCII.GetBytes("<th>");
        public static byte[] HeaderFieldEnd = Encoding.ASCII.GetBytes("</th>");
        public static byte[] HeaderStart = Encoding.ASCII.GetBytes("<thead><tr>");
        public static byte[] HeaderEnd = Encoding.ASCII.GetBytes("</tr></thead>");
        public static byte[] BodyStart = Encoding.ASCII.GetBytes("<tbody>");
        public static byte[] BodyEnd = Encoding.ASCII.GetBytes("</tbody>");
        public static byte[] RowStart = Encoding.ASCII.GetBytes("<tr>");
        public static byte[] RowEnd = Encoding.ASCII.GetBytes("</tr>");
        public static byte[] FieldStart = Encoding.ASCII.GetBytes("<td>");
        public static byte[] FieldEnd = Encoding.ASCII.GetBytes("</td>");
        public static byte[] IndexColumnName = Encoding.ASCII.GetBytes("Index");
        public static byte[] KeyColumnName = Encoding.ASCII.GetBytes("Key");
        public static byte[] EmptyString = Encoding.ASCII.GetBytes("");
        // When a cache entry is empty, or doesn't exist, then send a NoData
        // table header to Excel or browser. 
        public static byte[] NoDataTableHeader = Encoding.ASCII.GetBytes("<thead><tr><th>No cached data</th></tr></thead>");

        public static async Task FieldToStream(BizDeckLogger logger, byte[] field, Stream s, bool header = false) {
            byte[] start = FieldStart;
            byte[] end = FieldEnd;
            if (header) {
                start = HeaderFieldStart;
                end = HeaderFieldEnd;
            }
            await s.WriteAsync(start);
            if (field != null) {
                await s.WriteAsync(field);
            }
            else {
                logger.Error("FieldToStream: null field");
            }
            await s.WriteAsync(end);
        }

        public static async Task FieldToStream(BizDeckLogger logger, string field, Stream s, bool header = false) {
            byte[] bfield = Encoding.ASCII.GetBytes(field);
            await FieldToStream(logger, bfield, s, header);
        }

        public static async Task CacheEntryToStream(BizDeckLogger logger, CacheEntry ce, Stream s) {
            await s.WriteAsync(TableStart);
            if (ce != null && ce.Count > 0) {
                // More than one row, so  we will have ce.Headers for column names
                await s.WriteAsync(HeaderStart);
                // First column is index or row key
                await FieldToStream(logger, ce.GetKeyOrIndexColumnHeader(), s, true);
                foreach (string header in ce.Headers) {
                    // ce.RowKey will be "" for List entries, so all header cols will render
                    // But for Dict entries we don't want to repeat the first key field, and
                    // ce.RowKey will have a real value
                    if (header != ce.RowKey) {
                        await FieldToStream(logger, header, s, true);
                    }
                }
                await s.WriteAsync(HeaderEnd);
                // Column headers done, now for the data
                await s.WriteAsync(BodyStart);
                for (int index = 0; index < ce.Count; index++) {
                    CacheEntryRow row = ce.GetRow(index);
                    if (row != null) {
                        await s.WriteAsync(RowStart);
                        // Index or Key field first
                        await FieldToStream(logger, row.KeyValue, s);
                        foreach (string header in ce.Headers) {
                            if (header != ce.RowKey) {
                                await FieldToStream(logger, row.Row[header], s);
                            }
                        }
                        await s.WriteAsync(RowEnd);
                    }
                    else {
                        logger.Error($"CacheEntryToStream: no row for index[{index}]");
                    }
                }
                await s.WriteAsync(BodyEnd);
            }
            else {  // CacheEntry empty or not found
                await s.WriteAsync(NoDataTableHeader);
            }
            await s.WriteAsync(TableEnd);
        }
    }
}
