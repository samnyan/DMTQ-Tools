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
    class ItemDesc
    {
        public long item_id { get; set; }


        [FieldQuoted]
        public string name { get; set; }

        [FieldQuoted]
        public string description { get; set; }

        [FieldQuoted]
        public string summary { get; set; }
    }
}
