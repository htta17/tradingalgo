using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    public interface IATMStrategy
    {
        string FullATMName { get; }
        string HalfATMName { get; }
    }
}
