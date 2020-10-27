using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace COINS_ESB.Models
{
    public class CoinsArchiveXml
    {
        public long Id { get; set; }
        public DateTime RecDate { get; set; }
        public DateTime ArchiveDate { get; set; }
        public string RawXml { get; set; }
    }
}
