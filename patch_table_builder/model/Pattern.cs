using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileHelpers;


namespace patch_table_builder.model
{
    [IgnoreFirst(1)]
    [DelimitedRecord(",")]
    class Pattern
    {
        public long pattern_id { get; set; }
        public long song_id { get; set; }
        public long signature { get; set; }
        public int line { get; set; }
        public int difficulty { get; set; }
        public int point_type { get; set; }
        public int point_value { get; set; }
        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool flg { get; set; }
        public int update { get; set; }
    }
}
