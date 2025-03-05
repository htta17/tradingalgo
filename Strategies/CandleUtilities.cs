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
        public static bool IsRedCandle(double close, double open, double? minBody = 5, double? maxBody = 60, double? hi = null, double? low = null)
        {
            var ans = close < open;
            if (minBody.HasValue)
            {
                ans = ans && (open - close > minBody.Value);
            }
            if (maxBody.HasValue)
            {
                ans = ans && (open - close < maxBody.Value);
            }
            return ans;
        }

        /// <summary>
        /// Check 1 cây nến có phải là nến đỏ. 
        /// </summary>
        /// <param name="close">Giá đóng cây nến</param>
        /// <param name="open"></param>
        /// <param name="minBody">Giá tối thiểu của body</param>
        /// <param name="maxBody">Giá tối đa của body</param>
        /// <param name="hi"></param>
        /// <param name="low"></param>
        /// <returns></returns>
        public static bool IsGreenCandle(double close, double open, double? minBody = 5, double? maxBody = 60, double? hi = null, double? low = null)
        {
            var ans = close > open;
            if (minBody.HasValue)
            {
                ans = ans && (close - open > minBody.Value); 
            }
            if (maxBody.HasValue)
            { 
                ans = ans && (close - open < maxBody.Value);
            }
            return ans;
        }

        /// <summary>
        /// Tỉ lệ tính theo phần trăm của râu nến phía trên so với toàn bộ cây nến. 
        /// </summary>
        /// <param name="close"></param>
        /// <param name="open"></param>
        /// <param name="high"></param>
        /// <param name="low"></param>
        /// <returns></returns>
        public static double TopToBodyPercentage(double close, double open, double high, double low)
        {
            return (high - Math.Max(close, open)) / (high - low) * 100; 
        }

        /// <summary>
        /// Tỉ lệ tính theo phần trăm của râu nến phía trên so với toàn bộ cây nến. 
        /// </summary>
        /// <param name="close"></param>
        /// <param name="open"></param>
        /// <param name="high"></param>
        /// <param name="low"></param>
        /// <returns></returns>
        public static double BottomToBodyPercentage(double close, double open, double high, double low)
        {
            return (Math.Min(close, open) - low) / (high - low) * 100;
        }
    }
}
