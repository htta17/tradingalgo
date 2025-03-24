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
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Kitty : Rooster
	{
        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Cancel lệnh chờ nếu giá chạm Target 1",
            Description = "Cancel lệnh chờ nếu giá chạm Target 1",
            Order = 2, GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public bool AllowCancelWhenPriceMeetTarget1 { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Sử dụng thông tin RSI",
            Description = "Nếu TRUE: Sử dụng điều kiện RSI overbought hoặc oversold kết hợp với các điều kiện hiện tại để vào lệnh",
            Order = 2, GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public bool AllowUseRSIIndicator { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Ricky ATM Strategy", Description = "Ricky ATM Strategy", Order = 2,
            GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string RiskyAtmStrategyName { get; set; }

        protected AtmStrategy RiskyAtmStrategy { get; set; }

        public Kitty() : base("KITTY")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyKitty.txt");
        }

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            {
                RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyAtmStrategyName, Print).AtmStrategy;
            }
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Kitty";
            Description = "[Kitty] là giải thuật [Rooster], được viết riêng cho my love, Phượng Phan.";

            AllowCancelWhenPriceMeetTarget1 = false;

            RiskyAtmStrategyName = "Rooster_Risky";
            AllowUseRSIIndicator = false;
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
            var previous2WAE = waeValuesSeries[2];

            #region Trend SELL 
            /*
             * Điều kiện vào lệnh (SELL) 
             * 1. Volume ĐỎ, 
             * 2. 2 Volume ĐỎ liền nhau 
             * 3. (NOT IN USE) Volume sau cao hơn volume trước 
             * 4. Volume sau cao hơn DeadZone 
             * 5. Nến phải là nến ĐỎ, Thân nến > 5 points
             * 6. (NOT IN USE) Thân cây nến trước không quá 60pts
             * 7. (CONFIGUABLE) RSI > 30 (Not oversold)
             * 8. Râu nến phía DƯỚI không quá dài (Râu DƯỚI dài chứng tỏ có lực MUA mạnh, có thể đảo chiều)
             * 9. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là XANH và có body > 50% cây nến gần nhất.
             *          (Cây nến trước XANH chứng tỏ pull back, nếu pull back nhiều quá sẽ có thể đảo chiều)
             * 10. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là ĐỎ, body của cây nến gần nhất < 30% cây nến trước. 
             *          (Cây nến trước đã BÁN quá mạnh, cây nến vừa rồi lực BÁN đã suy giảm nhiều, có khả năng đảo chiều) 
             * 11. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Cây nến ĐỎ và có open > lower bollinger (std=2) và có close < lower bollinger (std=2)
             */

            const int PERCENTAGE_WICK_TO_TRADE = 70;
            const int RSI_TOO_BOUGHT = 70;
            const int RSI_TOO_SOLD = 30;

            var bottomToBodyPercent = CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m);
            var bottomToBody = bottomToBodyPercent < PERCENTAGE_WICK_TO_TRADE;
            var isRedCandle = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m);

            var isPreviousGreen = CandleUtilities.IsGreenCandle(prev_closePrice_5m, prev_openPrice_5m);
            var isPreviousRed = CandleUtilities.IsRedCandle(prev_closePrice_5m, prev_openPrice_5m);

            var previousBodyLength = Math.Abs(prev_openPrice_5m - prev_closePrice_5m);
            var currentBodyLength = Math.Abs(closePrice_5m - openPrice_5m);

            var previousReverseAndTooStrong = previousBodyLength >= (0.5 * currentBodyLength);

            var previousContinueAndTooStrong = (previousBodyLength * 0.3) >= currentBodyLength;

            var previousIsGreenAndTooStrong_FORSELL = isPreviousGreen && previousReverseAndTooStrong;

            var previousIsRedAndTooStrong_FORSELL = isPreviousRed && previousContinueAndTooStrong;

            var rsiTooSold = !AllowUseRSIIndicator || rsi_5m > RSI_TOO_SOLD; // Không dùng điều kiện RSI oversold hoặc nếu dùng thì phải thỏa mãn
            var rsiTooSoldText = !AllowUseRSIIndicator ? "7. (RSI Condition - NOT IN USE)" : $"7. RSI > {RSI_TOO_SOLD} (Not oversold): [{rsi_5m > RSI_TOO_SOLD}],";

            var additionalText = @$"
                        prev: (close: {prev_closePrice_5m:N2}, open: {prev_openPrice_5m:N2}, body: {previousBodyLength:N2}), 
                        current: (close: {closePrice_5m:N2}, open: {openPrice_5m:N2}, body: {currentBodyLength:N2}),  
                        Previous red: {isPreviousRed}, Previous green: {isPreviousGreen}";

            // Điều kiện về ngược trend: Cây nến đã vượt qua BollingerBand 
            var bodyPassBollingerDOWN = openPrice_5m > lowerStd2BB_5m && closePrice_5m < lowerStd2BB_5m;

            var continueRedTrending = currentWAE.DownTrendVal > 0 && (previousWAE.DownTrendVal > 0 || (previousWAE.UpTrendVal > 0 && previous2WAE.DownTrendVal > 0));

            var conditionForSell = currentWAE.HasBEARVolume && // 1 & 4
                previousWAE.DownTrendVal > 0 && //2
                                                //currentWAE.DownTrendVal > previousWAE.DownTrendVal && //3
                isRedCandle && // 5 
                               //previousBody && // 6
                rsiTooSold // 7
                           //bottomToBody && // 8
                           //!previousIsGreenAndTooStrong_FORSELL && // 9 (Don't forget NOT)
                           //!previousIsRedAndTooStrong_FORSELL && // 10 (Don't forget NOT)
                           //!bodyPassBollingerDOWN // 11 (Don't forget NOT)
                ;

            LocalPrint($@"
                Điều kiện vào SELL (Close: [{closePrice_5m:N2}], Open:[{openPrice_5m:N2}], Body: {Math.Abs(closePrice_5m - openPrice_5m):N2}): 
                1. Volume ĐỎ & cao hơn DeadZone: [{currentWAE.HasBEARVolume}],
                2. 2 Volume ĐỎ liền nhau hoặc 3 volume liền nhau thứ tự là ĐỎ - XANH - ĐỎ: [{continueRedTrending}],                 
                4. Volume sau cao hơn DeadZone: (See 1)
                5. Nến ĐỎ, Thân nến hiện tại > 5 points: [{isRedCandle}]
                {rsiTooSoldText}
                8. Râu nến phía DƯỚI không quá {PERCENTAGE_WICK_TO_TRADE}% toàn cây nến (Tỉ lệ hiện tại {bottomToBodyPercent}%): [{bottomToBody}].
                FINAL: [{conditionForSell}]");

            if (conditionForSell)
            {
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL();
                double priceToSet = GetSetPrice(TradeAction.Sell_Trending, atmStrategy);
                var target1 = priceToSet - atmStrategy.Brackets[0].Target * TickSize;

                /*Cả [priceToSet] và [target1] phải ở cùng bên của  [middleEma4651_5m] thì mới trade*/
                if ((target1 > middleEma4651_5m && priceToSet > middleEma4651_5m) || (target1 < middleEma4651_5m && priceToSet < middleEma4651_5m))
                {
                    FilledTime = Time[0];

                    return TradeAction.Sell_Trending;
                }
                else
                {
                    LocalPrint($"Target: {target1:N2}, Set Price: {priceToSet:N2}, EMA46/51: {middleEma4651_5m:N2}, có điều kiện để vào SELL nhưng gần với đường EMA46/51 --> No Trade for SELL");
                }
            }
            #endregion

            #region Trend BUY
            /*
             * Điều kiện vào lệnh (BUY) 
             * 1. Volume XANH, 
             * 2. 2 Volume XANH liền nhau 
             * 3. (NOT IN USE) Volume sau cao hơn volume trước 
             * 4. Volume sau cao hơn DeadZone 
             * 5. Nến phải là nến xanh, Thân nến > 5 points
             * 6. (NOT IN USE)  Thân cây nến trước không quá 60pts
             * 7. (CONFIGUABLE) RSI < 70 (Not overbought)
             * 8. Râu nến phía TRÊN không quá dài (Râu TRÊN dài chứng tỏ có lực BÁN mạnh, có thể đảo chiều)
             * 9. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là ĐỎ và có body > 50% cây nến gần nhất.
             *          (Cây nến trước ĐỎ chứng tỏ pull back, nếu pull back nhiều quá sẽ có thể đảo chiều)
             * 10. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là XANH, body của cây nến gần nhất < 30% cây nến trước. 
             *          (Cây nến trước đã MUA quá mạnh, cây nến vừa rồi lực MUA đã suy giảm nhiều, có khả năng đảo chiều) 
             * 11. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Cây nến XANH và có open < upper bollinger (std=2) và có close > upper bollinger (std=2)
             */
            var isGreenCandle = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m);

            var topToBodyPercent = CandleUtilities.TopToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m);
            var topToBody = topToBodyPercent < PERCENTAGE_WICK_TO_TRADE;

            var previousIsRedAndTooStrong_FORBUY = isPreviousRed && previousReverseAndTooStrong;

            var previousIsGreenAndTooStrong_FORBUY = isPreviousGreen && previousContinueAndTooStrong;

            // Điều kiện về ngược trend: Cây nến đã vượt qua BollingerBand 
            var bodyPassBollingerUP = openPrice_5m < upperStd2BB_5m && closePrice_5m > upperStd2BB_5m;

            var continueGreenTrending = currentWAE.UpTrendVal > 0 && (previousWAE.UpTrendVal > 0 || (previousWAE.DownTrendVal > 0 && previous2WAE.UpTrendVal > 0));

            var rsiTooBought = !AllowUseRSIIndicator || rsi_5m < RSI_TOO_BOUGHT; // Không dùng điều kiện RSI oversold hoặc nếu dùng thì phải thỏa mãn

            var rsiTooBoughtText = !AllowUseRSIIndicator ? "7. (RSI Condition - NOT IN USE)" : $"7. RSI < {RSI_TOO_BOUGHT} (Not overbought): [{rsi_5m < RSI_TOO_BOUGHT}],"; // Không dùng điều kiện RSI oversold hoặc nếu dùng thì phải thỏa mãn

            var conditionForBuy = currentWAE.HasBULLVolume && // 1 & 4
                previousWAE.UpTrendVal > 0 && //2
                                              //currentWAE.UpTrendVal > previousWAE.UpTrendVal && //3
                isGreenCandle && // 5
                                 //previousBody &&   // 6                
                rsiTooBought;// && // 7
                             //topToBody && //8
                             //!previousIsRedAndTooStrong_FORBUY &&  // 9 (Don't forget NOT)
                             //!previousIsGreenAndTooStrong_FORBUY && // 10 (Don't forget NOT)
                             //!bodyPassBollingerUP; // 11 (Don't forget NOT)

            LocalPrint($@"
                Điều kiện vào BUY (Close: [{closePrice_5m:N2}], Open:[{openPrice_5m:N2}], Body: {Math.Abs(closePrice_5m - openPrice_5m):N2}): 
                1. Volume XANH & cao hơn DeadZone: [{currentWAE.HasBULLVolume}],
                2. 2 Volume XANH liền nhau hoặc 3 volume liền nhau thứ tự là XANH - ĐỎ - XANH: [{continueGreenTrending}],                 
                4. Volume sau cao hơn DeadZone: (See 1)
                5. Nến XANH, Thân nến hiện tại > 5 points: [{isGreenCandle}]                
                {rsiTooBoughtText}
                8. Râu nến phía DƯỚI không quá {PERCENTAGE_WICK_TO_TRADE}% toàn cây nến (Tỉ lệ hiện tại {topToBodyPercent}%): [{topToBody}].
                FINAL: [{conditionForBuy}]");

            if (conditionForBuy)
            {
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL();
                double priceToSet = GetSetPrice(TradeAction.Buy_Trending, atmStrategy);
                var target1 = priceToSet + atmStrategy.Brackets[0].Target * TickSize;

                /*Cả [priceToSet] và [target1] phải ở cùng bên của  [middleEma4651_5m] thì mới trade*/
                if ((target1 > middleEma4651_5m && priceToSet > middleEma4651_5m) || (target1 < middleEma4651_5m && priceToSet < middleEma4651_5m))
                {
                    FilledTime = Time[0];

                    return TradeAction.Buy_Trending;
                }
                else
                {
                    LocalPrint($"Target: {target1:N2}, Set Price: {priceToSet:N2}, EMA46/51: {middleEma4651_5m:N2}, có điều kiện để vào BUY nhưng gần với đường EMA46/51 --> No Trade for BUY");
                }
            }
            #endregion

            #region Reversval 

            var totalMinutes = Time[0].Subtract(TouchEMA4651Time).TotalMinutes;
            var distanceToEMA = Math.Abs(middleEma4651_5m - currentPrice);

            if (totalMinutes > 60 && distanceToEMA < 20) // Nếu đã chạm EMA46/51 lâu rồi 
            {
                var logText = @$"
                    Last touch EMA46/51: {TouchEMA4651Time:HH:mm}, 
                    Total minutes until now:  {totalMinutes}, 
                    Distance to middle of EMA46/51: {distanceToEMA:N2}.";

                if (closePrice_5m > middleEma4651_5m && openPrice_5m > middleEma4651_5m)
                {
                    LocalPrint($"Đủ điều kiện cho BUY REVERSAL: {logText}");
                    return TradeAction.Buy_Reversal;
                }
                else if (closePrice_5m < middleEma4651_5m && openPrice_5m < middleEma4651_5m)
                {
                    LocalPrint($"Đủ điều kiện cho SELL REVERSAL: {logText}");
                    return TradeAction.Sell_Reversal;
                }
            }            
            #endregion

            return TradeAction.NoTrade;
        }

        protected override double GetSetPrice(TradeAction tradeAction, AtmStrategy atmStrategy)
        {
            if (tradeAction == TradeAction.Buy_Trending || tradeAction == TradeAction.Sell_Trending)
            {
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
            else // Reveral trade 
            {
                var setPrice = tradeAction == TradeAction.Buy_Reversal ?
                            Math.Min(currentPrice, middleEma4651_5m)
                            : Math.Max(currentPrice, middleEma4651_5m); 

                return StrategiesUtilities.RoundPrice(setPrice);
            }
        }

        protected override bool ShouldCancelPendingOrdersByTrendCondition()
        {
            // Nến gần nhất là ĐỎ hoặc nến rút râu phía trên
            var reverseRed = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m) || CandleUtilities.TopToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) > 50;

            // Nến gần nhất là ĐỎ hoặc nến rút râu phía trên
            var reverseGreen = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m) || CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) > 50;

            return  
                (IsBuying && reverseRed)        // Đang có lệnh MUA nhưng lại xuất hiện nến ĐỎ
                || (IsSelling && reverseGreen)  // Đang có lệnh BÁN nhưng lại xuất hiện nến XANH
                || base.ShouldCancelPendingOrdersByTrendCondition();
        }
        protected override void OnMarketData_DoForPendingFill(double updatedPrice)
        {
            if (!AllowCancelWhenPriceMeetTarget1)
            {
                return;
            }

            if ((IsBuying && updatedPrice > TargetPrice_Half))
            {
                LocalPrint($"Cancel lệnh BUY do giá đã chạm target 1. Giá hiện tại: {updatedPrice:N2}, TargetPrice_Half: {TargetPrice_Half:N2}");
                CancelAllPendingOrder(); 
            }
            else if (IsSelling && updatedPrice < TargetPrice_Half)
            {
                LocalPrint($"Cancel lệnh SELL do giá đã chạm target 1. Giá hiện tại: {updatedPrice:N2}, TargetPrice_Half: {TargetPrice_Half:N2}");
                CancelAllPendingOrder();
            }
        }

        protected override void MoveStopOrder(Order stopOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            double newPrice = -1;
            var allowMoving = false;
            var stopOrderPrice = stopOrder.StopPrice;

            // Dịch stop loss lên break even 
            if (isBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice > filledPrice && stopOrderPrice + PointToMoveLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveLoss;
                    allowMoving = true;
                }
                else if (filledPrice <= stopOrderPrice && stopOrderPrice <= TargetPrice_Half && TargetPrice_Half + 7 < updatedPrice)
                {
                    newPrice = TargetPrice_Half; 
                    allowMoving = true;
                }                
            }
            else if (isSelling)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice < filledPrice && stopOrderPrice - PointToMoveLoss > updatedPrice)
                {
                    newPrice = updatedPrice + PointToMoveLoss;
                    allowMoving = true;
                }
                else if (filledPrice >= stopOrderPrice && stopOrderPrice >= TargetPrice_Half && TargetPrice_Half - 7 > updatedPrice)
                {
                    newPrice = TargetPrice_Half;
                    allowMoving = true;
                }
            }

            if (allowMoving)
            {
                LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{updatedPrice}]");

                MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
            }
        }
    }

}
