using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    public class PriceActionUtilities
    {
    }

    [Flags]
    public enum PriceActionStrategy
    { 
        FairValueGap = 1, 

        BreakOut = 2, 

        BreakDown = 4, 

        Reversal = 8,
    }
}
