using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexCOINESB
{
    //These are the lines for the COINS P/O transfer status report
    public struct StatusLine
    {
        public string PO { get; set; }
        public string Job { get; set; }
        public string WorkOrd { get; set; }
        public string Vendor { get; set; }
        public string Message { get; set; }
    }
}
