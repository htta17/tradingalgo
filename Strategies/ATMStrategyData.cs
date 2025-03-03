using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NinjaTrader.Custom.Strategies
{
    [XmlRoot("NinjaTrader")]
    public class NinjaTraderConfig
    {
        public AtmStrategy AtmStrategy { get; set; }
    }
}
