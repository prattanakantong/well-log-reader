using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace well_log_reader_backend
{
    internal class Header
{
        public string mnem { get; set; }
        public string unit { get; set; }
        public string value { get; set; }
        public string description { get; set; }
        public int line { get; set; }
    }
}
