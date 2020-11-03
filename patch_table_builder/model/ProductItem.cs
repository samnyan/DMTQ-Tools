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
    class ProductItem
    {
        public long item_id { get; set; }

        [FieldQuoted]
        public string item_name { get; set; }

        [FieldQuoted]
        public string img_url_1 { get; set; }

        [FieldQuoted]
        public string img_url_2 { get; set; }
        
        [FieldQuoted]
        public string description { get; set; }
        
        public int repeat_count { get; set; }

        public string item_type { get; set; }

        public int limit_minute { get; set; }

        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool status { get; set; }
        
        public int buy_level { get; set; }

        public int buy_limit_count { get; set; }

        public string buy_limit_type { get; set; }

        [FieldQuoted]
        public string summary { get; set; }

        public int update { get; set; }
    }
}
