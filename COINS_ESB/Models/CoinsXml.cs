using System;

namespace COINS_ESB.Models
{
    public partial class CoinsXml
    {
        public long Id { get; set; }
        public DateTime RecDate { get; set; }
        public string RawXml { get; set; }
    }
}
