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
    class Patch
    {
        public string file_name { get; set; }
        public long file_size { get; set; }
        public string checksum { get; set; }
        public long compressed_file_size { get; set; }
        public string compressed_checksum { get; set; }
        public int acquire_on_demand { get; set; }
        public int compressed { get; set; }
        public string platform { get; set; }
        public string tag { get; set; }
        public string unused { get; set; }
    }
}
