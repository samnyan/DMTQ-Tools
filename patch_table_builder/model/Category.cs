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
    class Category
    {
        public long category_id { get; set; }

        public long product_id { get; set; }

        public long display_order { get; set; }

        public int update { get; set; }
    }
}
