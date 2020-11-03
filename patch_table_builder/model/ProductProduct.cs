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
    class ProductProduct
    {
        public long product_id { get; set; }

        public long item_id { get; set; }

        public long platform_product_id { get; set; }

        public string store_product_id { get; set; }

        public string product_type { get; set; }

        public int cost_game_point { get; set; }

        public int cost_game_cash { get; set; }
       
        public string status { get; set; }
        
        public string sale_start_date { get; set; }

        public string sale_end_date { get; set; }

        public int update { get; set; }
    }
}
