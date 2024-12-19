using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    public class StrategiesUtilities
    {
        private const int LongDiffAsBig = 15;
        private const int ShortDiffAsSmall = 3; 
        public static bool IsRedCandle(double open, double close)
        { 
            return close < open;
        }

        public static bool IsGreenCandle(double open, double close)
        {
            return close > open;
        }

        public static bool IsDojiCandle(double open, double close)
        {
            return Math.Abs(close - open) < ShortDiffAsSmall; // Almost the same
        }

        public static bool IsMarubozuCandle(double open, double high, double low, double close)
        {
            return Math.Abs(close - open) >= LongDiffAsBig // Thân dài
                && Math.Abs(high - Math.Max(open, close)) <= ShortDiffAsSmall // Body gần với High 
                && Math.Abs(Math.Min(open, close) - low) <= ShortDiffAsSmall;  // Body gần với Low
        }




    }
}
