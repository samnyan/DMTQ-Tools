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
    class Song
    {
        public long song_id { get; set; }

        public long item_id { get; set; }

        [FieldQuoted]
        public string name { get; set; }

        [FieldQuoted]
        public string full_name { get; set; }

        [FieldQuoted]
        public string genre { get; set; }
        
        [FieldQuoted]
        public string artist_name { get; set; }
        
        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool original_bga_yn { get; set; }
        
        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool loop_bga_yn { get; set; }
        
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
        
        public int cost_game_point { get; set; }
        
        public int cost_game_cash { get; set; }
        
        public int flag { get; set; }
        
        [FieldQuoted]
        public string status { get; set; }
        
        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool free_yn { get; set; }
        
        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool hidden_yn { get; set; }
        
        [FieldConverter(ConverterKind.Boolean, "Y", "N")]
        public bool open_yn { get; set; }
        
        public long track_id { get; set; }
        
        public string mod_date { get; set; }
        
        public int update { get; set; }
    }
}
