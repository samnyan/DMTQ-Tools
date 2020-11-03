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
    class SongDesc
    {
        public long song_id { get; set; }

        [FieldQuoted]
        public string full_name { get; set; }

        [FieldQuoted]
        public string genre { get; set; }
        
        [FieldQuoted]
        public string artist { get; set; }
        
        [FieldQuoted]
        public string composed_by { get; set; }
        
        [FieldQuoted]
        public string singer { get; set; }
        
        [FieldQuoted]
        public string feat_by { get; set; }
        
        [FieldQuoted]
        public string arranged_by { get; set; }
        
        [FieldQuoted]
        public string visualized_by { get; set; }
    }
}
