#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Custom.Strategies;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Kitty : RoosterATM
	{
        public Kitty() : base("KITTY")
        { 
        }

        protected override TradeAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return TradeAction.NoTrade;
            }

            var currentWAE = waeValuesSeries[0];
            var previousWAE = waeValuesSeries[1];

            /*
             * Điều kiện vào lệnh (SELL) 
             * 1. Volume ĐỎ, 
             * 2. 2 Volume ĐỎ liền nhau 
             * 3. Volume sau cao hơn volume trước 
             * 4. Volume sau cao hơn DeadZone 
             * 5. Nến phải là nến ĐỎ 
             * 6. Thân nến > 5 points 
             * 7. RSI > 30 (Not oversold)
             * 8. Râu nến phía DƯỚI không quá dài (Râu DƯỚI dài chứng tỏ có lực MUA mạnh, có thể đảo chiều)
             */
            if (currentWAE.HasBEARVolume && // 1 & 4
                previousWAE.DownTrendVal > 0 && //2
                currentWAE.DownTrendVal > previousWAE.DownTrendVal && //3
                CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m, 5.0) && // 5 & 6
                rsi_5m > 30 && // 7
                CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) < 40)
            {
                LocalPrint($"Found SELL signal (Trending) - waeDeadVal_5m: {waeDeadVal_5m:N2}, waeDowntrend_5m: {waeDowntrend_5m:N2}, " +
                    $"waeDeadVal_5m[-1]: {waeValuesSeries[1].DeadZoneVal:N2}, waeDowntrend_5m[-1]: {waeValuesSeries[1].DownTrendVal:N2}");

                FilledTime = Time[0];

                return TradeAction.Sell_Trending;
            }

            /*
             * Điều kiện vào lệnh (BUY) 
             * 1. Volume XANH, 
             * 2. 2 Volume XANH liền nhau 
             * 3. Volume sau cao hơn volume trước 
             * 4. Volume sau cao hơn DeadZone 
             * 5. Nến phải là nến xanh 
             * 6. Thân nến > 5 points 
             * 7. RSI < 70 (Not overbought)
             * 8. Râu nến phía TRÊN không quá dài (Râu TRÊN dài chứng tỏ có lực BÁN mạnh, có thể đảo chiều)
             */
            else if (currentWAE.HasBULLVolume && // 1 & 4
                previousWAE.UpTrendVal > 0 && //2
                currentWAE.UpTrendVal > previousWAE.UpTrendVal && //3
                CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m, 5.0) && // 5 & 6
                rsi_5m < 70 && // 7
                CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) < 40
                )
            {
                LocalPrint($"Found BUY signal (Trending) - waeDeadVal_5m: {waeDeadVal_5m:N2}, waeDowntrend_5m: {waeUptrend_5m:N2}, " +
                    $"waeDeadVal_5m[-1]: {waeValuesSeries[1].DeadZoneVal:N2}, waeDowntrend_5m[-1]: {waeValuesSeries[1].UpTrendVal:N2}");

                FilledTime = Time[0];

                return TradeAction.Buy_Trending;
            }            

            return TradeAction.NoTrade;
        }

        protected override double GetSetPrice(TradeAction tradeAction)
        {   
            // Nếu volume đang yếu hoặc Medium thì 
            var volumeStrength = waeValuesSeries[0].WAE_Strength;
            LocalPrint($"Volume Strength: SUM: {(waeValuesSeries[0].DownTrendVal + waeValuesSeries[0].UpTrendVal):N2}, [{volumeStrength.ToString()}]");

            // Tìm điểm vào lệnh thích hợp. 
            // Nếu cây nến hiện tại cùng chiều market (Red khi bearish, hoặc Green khi bullish) 
            var wholeBody = Math.Abs(closePrice_5m - openPrice_5m);
            // Hệ số (so với cây nến trước): Lấy 1/2 nếu Strong, 1/3 nếu Super Strong
            var coeff = 
                volumeStrength == WAE_Strength.Weak || volumeStrength == WAE_Strength.Medium || volumeStrength == WAE_Strength.Strong ? 2.0 
                : volumeStrength == WAE_Strength.SuperStrong ? 3.0 : 4.0;

            if (tradeAction == TradeAction.Buy_Trending)
            {
                // Đặt lệnh BUY với 1/3 cây nến trước đó 
                return StrategiesUtilities.RoundPrice(closePrice_5m - (wholeBody / coeff));
            }
            else // SELL 
            {
                // Đặt lệnh SELL với 1/3 cây nến trước đó 
                return StrategiesUtilities.RoundPrice(closePrice_5m + (wholeBody / coeff));
            }
        }

        protected override bool ShouldCancelPendingOrdersByTrendCondition()
        {
            return  
                (IsBuying && CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m))        // Đang có lệnh MUA nhưng lại xuất hiện nến ĐỎ
                || (IsSelling && CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m))  // Đang có lệnh BÁN nhưng lại xuất hiện nến XANH
                || base.ShouldCancelPendingOrdersByTrendCondition();
        }
    }
}
