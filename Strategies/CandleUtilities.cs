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
            return Math.Abs(Close - Open) <= (High - Low) * 0.07;
        }
    }
}
