using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{    
    public class Candle
    {
        public Candle(double open, double close, double high, double low) 
        { 
            Open = open;
            Close = close;
            High = high;
            Low = low;
        }

        public double Open { get; private set; }
        public double Close { get; private set; }
        public double High { get; private set; }
        public double Low { get; private set; }

        /// <summary>
        /// Kiểm tra cây nến có phải là nến Doji không
        /// </summary>
        /// <returns></returns>
        public bool IsDoji()
        {
            // Copied from @CandleStickPattern
            return Math.Abs(Close - Open) <= (High - Low) * 0.07;
        }

        public bool IsHammer()
        {
            // Copied from ChatGPT
            // Không sử dụng điều kiện Close[0] > Close[1]
            return Close > Open && // Bullish body (Close > Open)
                Open - Low > 2 * Close - Open && // Long lower wick
                High - Close < (Close - Open) * 0.2;  // Small upper wick
                // Close[0] > Close[1] // Close is higher than previous close (confirmation)
        }
    }

    public class CandleUtilities
    {
        public static bool IsRedCandle(double close, double open, double? hi = null, double? low = null)
        {
            return close < open; 
        }

        public static bool IsGreenCandle(double close, double open, double? hi = null, double? low = null)
        { 
            return close > open;
        }

    }
}
