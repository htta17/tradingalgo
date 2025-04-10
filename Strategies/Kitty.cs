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
	public abstract class Kitty : Rooster
	{
#if USE_RSI_TO_ENTER_ORDER
        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Sử dụng thông tin RSI",
            Description = "Nếu TRUE: Sử dụng điều kiện RSI overbought hoặc oversold kết hợp với các điều kiện hiện tại để vào lệnh",
            Order = 2, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public bool AllowUseRSIIndicator { get; set; }
#endif

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Ricky ATM Strategy", Description = "Ricky ATM Strategy", Order = 2,
            GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string RiskyAtmStrategyName { get; set; }

        protected AtmStrategy RiskyAtmStrategy { get; set; }

        /// <summary>
        /// Chốt lời khi cây nến lớn hơn giá trị này (points), current default value: 60 points.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Chốt lời khi cây nến > (points):",
            Description = "Nếu TRUE: Nếu cây nến lớn hơn [giá trị] thì đóng lệnh.",
            Order = 2, GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]        
        public int CloseOrderWhenCandleGreaterThan { get; set; }

        public Kitty() : base("KITTY")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyKitty.txt");
            Configured_TimeFrameToTrade = TimeFrameToTrade.FiveMinutes;
        }

        protected override void OnStateChange_Configure()
        {
            base.OnStateChange_Configure();

            RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyAtmStrategyName, Print).AtmStrategy;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Kitty";
            Description = "[Kitty] là giải thuật [Rooster], được viết riêng cho my love, Phượng Phan.";

            FullSizeATMName = "Kitty_Default_4cts";
            HalfSizefATMName = "Kitty_Default_2cts";
            RiskyAtmStrategyName = "Kitty_Risky";

#if USE_RSI_TO_ENTER_ORDER
            AllowUseRSIIndicator = false;
#endif

            StartDayTradeTime = new TimeSpan(9, 10, 0); // 9:10:00 am 
            EndDayTradeTime = new TimeSpan(15, 0, 0); // 2:00:00 pm

            CloseOrderWhenCandleGreaterThan = 60; // 60 điểm
        }

        protected override TradeAction ShouldTrade()
        {
            if (Time[0].TimeOfDay < StartDayTradeTime || Time[0].TimeOfDay > EndDayTradeTime)
            {
                LocalPrint($"Thời gian trade được thiết lập từ {StartDayTradeTime} to {EndDayTradeTime} --> No Trade.");
                return TradeAction.NoTrade;
            }

            //var currentWAE = waeValuesSeries_5m[0];

            //if (currentWAE.HasBULLVolume)
            //{
            //    return TradeAction.Buy_Trending;
            //}
            //else if (currentWAE.HasBEARVolume)
            //{
            //    return TradeAction.Sell_Trending;
            //}

            return TradeAction.NoTrade;         
        }

#if USE_WAE
        /// <summary>
        /// Old Function, để đó lỡ cần tham khảo đến sau này. 
        /// </summary>
        /// <returns></returns>
        protected TradeAction ShouldTrade_OLD()
        {
            if (Time[0].TimeOfDay < StartDayTradeTime || Time[0].TimeOfDay > EndDayTradeTime)
            {
                LocalPrint($"Thời gian trade được thiết lập từ {StartDayTradeTime} to {EndDayTradeTime} --> No Trade.");
                return TradeAction.NoTrade;
            }

            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return TradeAction.NoTrade;
            }

            var currentWAE = waeValuesSeries_5m[0];
            var previousWAE = waeValuesSeries_5m[1];
            var previous2WAE = waeValuesSeries_5m[2];

            var currentWAE_15m = wAE_ValueSet_15m;

            #region Trend SELL 
            /*
             * Điều kiện vào lệnh (SELL) 
             * 1. Volume ĐỎ, 
             * 2. 2 Volume ĐỎ liền nhau 
             * 3. (NOT IN USE) Volume sau cao hơn volume trước 
             * 4. Volume sau cao hơn DeadZone 
             * 5. Nến phải là nến ĐỎ, thân nến > 5 points và < 60 pts
             * 6. (NOT IN USE) Thân cây nến trước không quá 60pts
             * 6. Cây nến trước đó cũng là nến ĐỎ
             * 7. (CONFIGUABLE) RSI > 30 (Not oversold)
             * 8. Râu nến phía DƯỚI không quá dài (Râu DƯỚI dài chứng tỏ có lực MUA mạnh, có thể đảo chiều)
             * 9. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là XANH và có body > 50% cây nến gần nhất.
             *          (Cây nến trước XANH chứng tỏ pull back, nếu pull back nhiều quá sẽ có thể đảo chiều)
             * 10. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là ĐỎ, body của cây nến gần nhất < 30% cây nến trước. 
             *          (Cây nến trước đã BÁN quá mạnh, cây nến vừa rồi lực BÁN đã suy giảm nhiều, có khả năng đảo chiều) 
             * 11. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Cây nến ĐỎ và có open > lower bollinger (std=2) và có close < lower bollinger (std=2)             
             * 12. VOLUME KHUNG 15 phút phải là ĐỎ. 
             */

            const int PERCENTAGE_WICK_TO_TRADE = 70;
            const int RSI_TOO_BOUGHT = 70;
            const int RSI_TOO_SOLD = 30;
            const int MIN_BODY_LENGTH_TO_TRADE = 5;
            const int MAX_BODY_LENGTH_TO_TRADE = 60;

            var bottomToBodyPercent = CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m);
            var bottomToBody = bottomToBodyPercent < PERCENTAGE_WICK_TO_TRADE;
            var isRedCandle = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m, MIN_BODY_LENGTH_TO_TRADE, MAX_BODY_LENGTH_TO_TRADE);

            var isPreviousGreen = CandleUtilities.IsGreenCandle(prev_closePrice_5m, prev_openPrice_5m, null, MAX_BODY_LENGTH_TO_TRADE);
            var isPreviousRed = CandleUtilities.IsRedCandle(prev_closePrice_5m, prev_openPrice_5m, null, MAX_BODY_LENGTH_TO_TRADE);

            var previousBodyLength = Math.Abs(prev_openPrice_5m - prev_closePrice_5m);
            var currentBodyLength = Math.Abs(closePrice_5m - openPrice_5m);

            var previousReverseAndTooStrong = previousBodyLength >= (0.5 * currentBodyLength);

            var previousContinueAndTooStrong = (previousBodyLength * 0.3) >= currentBodyLength;

            var previousIsGreenAndTooStrong_FORSELL = isPreviousGreen && previousReverseAndTooStrong;

            var previousIsRedAndTooStrong_FORSELL = isPreviousRed && previousContinueAndTooStrong;

            //var redVolume_15m = wAE_ValueSet_15m.DownTrendVal > 0;            

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
                isRedCandle && isPreviousRed && // 5 
                               //previousBody && // 6
                rsiTooSold; // 7
                              //bottomToBody && // 8
                              //!previousIsGreenAndTooStrong_FORSELL && // 9 (Don't forget NOT)
                              //!previousIsRedAndTooStrong_FORSELL && // 10 (Don't forget NOT)
                              //!bodyPassBollingerDOWN // 11 (Don't forget NOT)
                              //redVolume_15m;

            LocalPrint($@"
                Điều kiện vào SELL (Close: [{closePrice_5m:N2}], Open:[{openPrice_5m:N2}], Body: {Math.Abs(closePrice_5m - openPrice_5m):N2}): 
                1. Volume ĐỎ & cao hơn DeadZone: [{currentWAE.HasBEARVolume}],
                2. 2 Volume ĐỎ liền nhau hoặc 3 volume liền nhau thứ tự là ĐỎ - XANH - ĐỎ: [{continueRedTrending}],                 
                4. Volume sau cao hơn DeadZone: (See 1)
                5. Nến ĐỎ, Thân nến hiện tại > {MIN_BODY_LENGTH_TO_TRADE} points và < {MAX_BODY_LENGTH_TO_TRADE} pts: [{isRedCandle}]
                6. Cây nến trước đó cũng là nến ĐỎ, > {MIN_BODY_LENGTH_TO_TRADE} points và < {MAX_BODY_LENGTH_TO_TRADE} pts: [{isPreviousRed}]
                {rsiTooSoldText}
                8. Râu nến phía DƯỚI không quá {PERCENTAGE_WICK_TO_TRADE}% toàn cây nến (Tỉ lệ hiện tại {bottomToBodyPercent:N2}%): [{bottomToBody}].                
                FINAL: [{conditionForSell}]");

            if (conditionForSell)
            {
                /*
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(TradeAction.Sell_Trending);
                double priceToSet = GetSetPrice(TradeAction.Sell_Trending, atmStrategy);
                var target1 = priceToSet - atmStrategy.Brackets[0].Target * TickSize;

                // Cả [priceToSet] và [target1] phải ở cùng bên của  [middleEma4651_5m] thì mới trade
                if ((target1 > middleEma4651_5m && priceToSet > middleEma4651_5m) || (target1 < middleEma4651_5m && priceToSet < middleEma4651_5m))
                {
                */
                    
                    FilledTime = Time[0];

                    return TradeAction.Sell_Trending;
                /*
                }
                else
                {
                    LocalPrint($"Target: {target1:N2}, Set Price: {priceToSet:N2}, EMA46/51: {middleEma4651_5m:N2}, có điều kiện để vào SELL nhưng gần với đường EMA46/51 --> No Trade for SELL");
                }
                */
            }
            #endregion

            #region Trend BUY
            /*
             * Điều kiện vào lệnh (BUY) 
             * 1. Volume XANH, 
             * 2. 2 Volume XANH liền nhau 
             * 3. (NOT IN USE) Volume sau cao hơn volume trước 
             * 4. Volume sau cao hơn DeadZone 
             * 5. Nến phải là nến xanh, thân nến > 5 points và < 60 pts
             * 6. (NOT IN USE)  Thân cây nến trước không quá 60pts
             * 6. Cây nến trước đó cũng là nến XANH
             * 7. (CONFIGUABLE) RSI < 70 (Not overbought)
             * 8. Râu nến phía TRÊN không quá dài (Râu TRÊN dài chứng tỏ có lực BÁN mạnh, có thể đảo chiều)
             * 9. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là ĐỎ và có body > 50% cây nến gần nhất.
             *          (Cây nến trước ĐỎ chứng tỏ pull back, nếu pull back nhiều quá sẽ có thể đảo chiều)
             * 10. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Nến trước là XANH, body của cây nến gần nhất < 30% cây nến trước. 
             *          (Cây nến trước đã MUA quá mạnh, cây nến vừa rồi lực MUA đã suy giảm nhiều, có khả năng đảo chiều) 
             * 11. (NOT IN USE) KHÔNG ĐƯỢC THỎA MÃN điều kiện: Cây nến XANH và có open < upper bollinger (std=2) và có close > upper bollinger (std=2)
             * 12. VOLUME KHUNG 15 phút phải là XANH. 
             */
            var isGreenCandle = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m, MIN_BODY_LENGTH_TO_TRADE, MAX_BODY_LENGTH_TO_TRADE);

            var topToBodyPercent = CandleUtilities.TopToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m);
            var topToBody = topToBodyPercent < PERCENTAGE_WICK_TO_TRADE;

            var previousIsRedAndTooStrong_FORBUY = isPreviousRed && previousReverseAndTooStrong;

            var previousIsGreenAndTooStrong_FORBUY = isPreviousGreen && previousContinueAndTooStrong;

            // Điều kiện về ngược trend: Cây nến đã vượt qua BollingerBand 
            var bodyPassBollingerUP = openPrice_5m < upperStd2BB_5m && closePrice_5m > upperStd2BB_5m;

            var continueGreenTrending = currentWAE.UpTrendVal > 0 && (previousWAE.UpTrendVal > 0 || (previousWAE.DownTrendVal > 0 && previous2WAE.UpTrendVal > 0));

            var rsiTooBought = !AllowUseRSIIndicator || rsi_5m < RSI_TOO_BOUGHT; // Không dùng điều kiện RSI oversold hoặc nếu dùng thì phải thỏa mãn

            var rsiTooBoughtText = !AllowUseRSIIndicator ? "7. (RSI Condition - NOT IN USE)" : $"7. RSI < {RSI_TOO_BOUGHT} (Not overbought): [{rsi_5m < RSI_TOO_BOUGHT}],"; // Không dùng điều kiện RSI oversold hoặc nếu dùng thì phải thỏa mãn

            //var greenVolume_15m = wAE_ValueSet_15m.UpTrendVal > 0;

            var conditionForBuy = currentWAE.HasBULLVolume && // 1 & 4
                previousWAE.UpTrendVal > 0 && //2
                                              //currentWAE.UpTrendVal > previousWAE.UpTrendVal && //3
                isGreenCandle && isPreviousGreen &&// 5
                         //previousBody &&   // 6                
                rsiTooBought; //&& // 7
                                //topToBody && //8
                                //!previousIsRedAndTooStrong_FORBUY &&  // 9 (Don't forget NOT)
                                //!previousIsGreenAndTooStrong_FORBUY && // 10 (Don't forget NOT)
                                //!bodyPassBollingerUP; // 11 (Don't forget NOT)
                // greenVolume_15m;

            LocalPrint($@"
                Điều kiện vào BUY (Close: [{closePrice_5m:N2}], Open:[{openPrice_5m:N2}], Body: {Math.Abs(closePrice_5m - openPrice_5m):N2}): 
                1. Volume XANH & cao hơn DeadZone: [{currentWAE.HasBULLVolume}],
                2. 2 Volume XANH liền nhau hoặc 3 volume liền nhau thứ tự là XANH - ĐỎ - XANH: [{continueGreenTrending}],                 
                4. Volume sau cao hơn DeadZone: (See 1)
                5. Nến XANH, thân nến hiện tại > {MIN_BODY_LENGTH_TO_TRADE} points và < {MAX_BODY_LENGTH_TO_TRADE} pts: [{isGreenCandle}]      
                6. Cây nến trước đó cũng là nến XANH, > {MIN_BODY_LENGTH_TO_TRADE} points và < {MAX_BODY_LENGTH_TO_TRADE} pts: [{isPreviousGreen}]
                {rsiTooBoughtText}
                8. Râu nến phía TRÊN không quá {PERCENTAGE_WICK_TO_TRADE}% toàn cây nến (Tỉ lệ hiện tại {topToBodyPercent:N2}%): [{topToBody}].               
                FINAL: [{conditionForBuy}]");

            if (conditionForBuy)
            {
                /*
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(TradeAction.Buy_Trending);
                double priceToSet = GetSetPrice(TradeAction.Buy_Trending, atmStrategy);
                var target1 = priceToSet + atmStrategy.Brackets[0].Target * TickSize;

                // Cả [priceToSet] và [target1] phải ở cùng bên của  [middleEma4651_5m] thì mới trade
                if ((target1 > middleEma4651_5m && priceToSet > middleEma4651_5m) || (target1 < middleEma4651_5m && priceToSet < middleEma4651_5m))
                {
                */
                    FilledTime = Time[0];

                    return TradeAction.Buy_Trending;
                /*
                }
                else
                {
                    LocalPrint($"Target: {target1:N2}, Set Price: {priceToSet:N2}, EMA46/51: {middleEma4651_5m:N2}, có điều kiện để vào BUY nhưng gần với đường EMA46/51 --> No Trade for BUY");
                }
                */
            }
            #endregion

            /*
            #region Reversval (Same as Rooster)
            var totalMinutes = Time[0].Subtract(TouchEMA4651Time).TotalMinutes;
            var distanceToEMA = Math.Abs(middleEma4651_5m - currentPrice);
            var tradeReversal = totalMinutes > 60 && distanceToEMA < 20;

            var logText = @$"
                    Last touch EMA46/51: {TouchEMA4651Time:HH:mm}, 
                    Total minutes until now:  {totalMinutes}, 
                    Distance to middle of EMA46/51: {distanceToEMA:N2}.
                    --> Trade REVERSAL (totalMinutes > 60 && distanceToEMA < 20): {tradeReversal}";

            LocalPrint(logText);

            if (tradeReversal) // Nếu đã chạm EMA46/51 lâu rồi 
            {
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
            */

            return TradeAction.NoTrade;
        }
#endif

        protected override (AtmStrategy, string) GetAtmStrategyByPnL(TradeAction tradeAction)
        {
            /*
             * Các trường hợp risky
             */

            // Trường hợp 1: Cây nến có thân nhỏ hơn cả râu trên và râu phía dưới. 
            var currentBodyLength = Math.Abs(closePrice_5m - openPrice_5m);
            var bodyIsSmallerThanOthers = currentBodyLength < (highPrice_5m - Math.Max(closePrice_5m, openPrice_5m))
                && currentBodyLength < (Math.Min(closePrice_5m, openPrice_5m) - lowPrice_5m);
            
            if (bodyIsSmallerThanOthers)
            {
                return (RiskyAtmStrategy, RiskyAtmStrategyName);
            }            

            // Trường hợp 2: Giá vào lệnh và target băng qua đường EMA46/51. 
            // Đây là 1 key quan trọng nên rất dễ bị reverse. 

            // Lấy thử giá (giả sử là full size) 
            var assumeAtmStrategy = FullSizeAtmStrategy; 
            var assumeEntryPrice = GetSetPrice(tradeAction, assumeAtmStrategy);
            var target1 = tradeAction == TradeAction.Buy_Trending ? assumeEntryPrice + assumeAtmStrategy.Brackets[0].Target * TickSize
                : assumeEntryPrice - assumeAtmStrategy.Brackets[0].Target * TickSize;

            //  Giá vào lệnh và target băng qua đường EMA46/51. 
            if ((target1 < middleEma4651_5m && assumeEntryPrice > middleEma4651_5m) || (target1 > middleEma4651_5m && assumeEntryPrice < middleEma4651_5m))
            {
                return (RiskyAtmStrategy, RiskyAtmStrategyName);
            }

            // Trường hợp 3: Volume giảm dần (NOT IN USE)
            /*
            var currentWAE = waeValuesSeries[0];
            var previousWAE = waeValuesSeries[1];
            var previous2WAE = waeValuesSeries[2];

            var descreaseBULLVolume = tradeAction == TradeAction.Buy_Trending &&
                (currentWAE.UpTrendVal < previousWAE.UpTrendVal || (previousWAE.UpTrendVal < previous2WAE.UpTrendVal && previous2WAE.UpTrendVal > 0));
            var descreaseBEARVolume = tradeAction == TradeAction.Sell_Trending &&
                (currentWAE.DownTrendVal < previousWAE.DownTrendVal || (previousWAE.DownTrendVal < previous2WAE.DownTrendVal && previous2WAE.DownTrendVal > 0));
            */

            var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachHalf =
                (todaysPnL <= (-MaximumDailyLoss / 2)) || (todaysPnL >= (DailyTargetProfit / 2));

            return reachHalf ? (HalfSizeAtmStrategy, HalfSizefATMName) : (FullSizeAtmStrategy, FullSizeATMName);
        }

        protected override double GetSetPrice(TradeAction tradeAction, AtmStrategy atmStrategy)
        {
#if USE_WAE
            if (tradeAction == TradeAction.Buy_Trending || tradeAction == TradeAction.Sell_Trending)
            {
                // High risk 

                /*
                var currentBodyLength = Math.Abs(closePrice_5m - openPrice_5m);
                var bodyIsSmallerThanOthers = currentBodyLength < (highPrice_5m - Math.Max(closePrice_5m, openPrice_5m))
                    && currentBodyLength < (Math.Min(closePrice_5m, openPrice_5m) - lowPrice_5m);

                if (bodyIsSmallerThanOthers)
                {
                    if (tradeAction == TradeAction.Buy_Trending)
                    {
                        LocalPrint($"Body length nhỏ hơn 2 râu phía trên và dưới, vào lệnh BUY theo giá LOW của cây nến trước {lowPrice_5m:N2}");
                        // Đặt lệnh BUY với 1/3 cây nến trước đó 
                        return StrategiesUtilities.RoundPrice(lowPrice_5m);
                    }
                    else // SELL 
                    {
                        LocalPrint($"Body length nhỏ hơn 2 râu phía trên và dưới, vào lệnh SELL theo giá HIGH của cây nến trước {highPrice_5m:N2}");
                        // Đặt lệnh SELL với 1/3 cây nến trước đó 
                        return StrategiesUtilities.RoundPrice(highPrice_5m);
                    }
                }
                */

                // Low risk 
                var volumeStrength = waeValuesSeries_5m[0].WAE_Strength;
                LocalPrint($"Volume Strength: SUM: {(waeValuesSeries_5m[0].DownTrendVal + waeValuesSeries_5m[0].UpTrendVal):N2}, [{volumeStrength.ToString()}]");

                /*
                // Tìm điểm vào lệnh thích hợp. 
                // Nếu cây nến hiện tại cùng chiều market (Red khi bearish, hoặc Green khi bullish) 
                
                // Hệ số (so với cây nến trước): Lấy 1/2 nếu Strong, 1/3 nếu Super Strong
                //var coeff = 
                //    volumeStrength == WAE_Strength.Weak || volumeStrength == WAE_Strength.Medium || volumeStrength == WAE_Strength.Strong ? 2.0 : 3.0;                
                */

                if (volumeStrength == WAE_Strength.SuperWeak || volumeStrength == WAE_Strength.Weak)
                {
                    return StrategiesUtilities.RoundPrice(ema10_5m);
                }
                else if (volumeStrength == WAE_Strength.Medium)
                {
                    return StrategiesUtilities.RoundPrice((ema29_1m + ema10_5m) / 2);
                }
                else if (volumeStrength == WAE_Strength.MediumStrong)
                {
                    return StrategiesUtilities.RoundPrice(ema29_1m);
                }
                else if (volumeStrength == WAE_Strength.Strong)
                {
                    return StrategiesUtilities.RoundPrice(ema21_1m);
                }
                else // SuperStrong
                {
                    var wholeBody = Math.Abs(closePrice_5m - openPrice_5m);

                    if (tradeAction == TradeAction.Buy_Trending)
                    {
                        // Đặt lệnh BUY với 1/3 cây nến trước đó 
                        return StrategiesUtilities.RoundPrice(closePrice_5m - (wholeBody / 3));
                    }
                    else // SELL 
                    {
                        // Đặt lệnh SELL với 1/3 cây nến trước đó 
                        return StrategiesUtilities.RoundPrice(closePrice_5m + (wholeBody / 3));
                    }
                }
            }
            else // Reveral trade 
            {
                var setPrice = tradeAction == TradeAction.Buy_Reversal ?
                            Math.Min(currentPrice, middleEma4651_5m)
                            : Math.Max(currentPrice, middleEma4651_5m);

                return StrategiesUtilities.RoundPrice(setPrice);
            }
#endif
            return StrategiesUtilities.RoundPrice((ema29_1m + ema10_5m) / 2);
        }

        protected override bool ShouldCancelPendingOrdersByTrendCondition()
        {
            if (IsTrendingTrade)
            {
                // Nến gần nhất là ĐỎ hoặc nến rút râu phía trên
                var reverseRed = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m) || CandleUtilities.TopToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) > 50;

                // Nến gần nhất là ĐỎ hoặc nến rút râu phía dưới
                var reverseGreen = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m) || CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) > 50;

                if (IsBuying && reverseRed)
                {
                    LocalPrint($"Đang có lệnh MUA nhưng lại xuất hiện nến ĐỎ hoặc hoặc nến rút râu phía trên (>50%)");
                    return true;
                }

                if (IsSelling && reverseGreen)
                {
                    LocalPrint($"Đang có lệnh BÁN nhưng lại xuất hiện nến XANH hoặc nến rút râu phía dưới (>50%)");
                    return true;
                }

                return base.ShouldCancelPendingOrdersByTrendCondition();
            }

            return false;
        }


        protected override void UpdateExistingOrder()
        {
            LocalPrint("[UpdateExistingOrder - Kitty] - NOT Using now");
            return; 

            // NOT USE NOW
            
            /*
             * Giải thuật hiện tại của Kitty: 
             * - Sau khi ATM đưa stop loss về break even
             * - Nếu đóng nến xanh (khi đang có lệnh mua) thì chuyển stop loss, nếu nến xanh > 60 pts thì đóng lệnh
             * - Nếu đóng nến đỏ (khi đang có lệnh bán) thì chuyển stop loss, nếu nến đỏ > 60 pts thì đóng lệnh
             * 
             */
            var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains(OrderStopName)).ToList();            

            var stopOrder = stopOrders.First();

            if (stopOrders.Count == 1)
            {
                LocalPrint($"Có {stopOrders.Count} stop order, should move stop loss if pass condition.");

                var stopOrderPrice = stopOrder.LimitPrice;

                var filledPrice = FilledPrice;

                bool allowMoving = false;                
                double newPrice = -1;
                var bodyLength = Math.Abs(closePrice_5m - openPrice_5m);

                if (IsBuying && filledPrice <= stopOrderPrice && CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m, null, null))
                {
                    if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                    {
                        LocalPrint($"Nến xanh có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                        CloseExistingOrders();
                    }
                    else if (bodyLength > 12)
                    {
                        var newStopLossBasedOnGreenCandle = StrategiesUtilities.RoundPrice(openPrice_5m + Math.Abs(closePrice_5m - openPrice_5m) / 3);

                        allowMoving = stopOrderPrice < newStopLossBasedOnGreenCandle;

                        if (allowMoving)
                        {
                            LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnGreenCandle:N2}");
                            newPrice = newStopLossBasedOnGreenCandle;
                        }
                    }
                }
                else if (IsSelling && filledPrice >= stopOrderPrice && CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m, null, null))
                {
                    if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                    {
                        LocalPrint($"Nến đỏ có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                        CloseExistingOrders();
                    }
                    else if (bodyLength > 12)
                    {
                        var newStopLossBasedOnRedCandle = StrategiesUtilities.RoundPrice(openPrice_5m - Math.Abs(closePrice_5m - openPrice_5m) / 3);

                        allowMoving = stopOrderPrice > newStopLossBasedOnRedCandle;

                        if (allowMoving)
                        {
                            LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnRedCandle:N2}");
                            newPrice = newStopLossBasedOnRedCandle;
                        }
                    } 
                }

                if (allowMoving)
                {
                    LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{currentPrice}]");

                    MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
                }
            }
        }

        protected override void MoveStopOrder(Order stopOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            double newPrice = -1;
            var allowMoving = false;
            var stopOrderPrice = stopOrder.StopPrice;            
            
            if (isBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice > filledPrice && stopOrderPrice + PointToMoveLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveLoss;
                    allowMoving = true;
                }
                else if (updatedPrice - filledPrice >= 60 && stopOrderPrice - filledPrice < 40)
                {
                    newPrice = filledPrice + 40; 
                    allowMoving = true;
                }
                else if (updatedPrice - filledPrice >= 30 && stopOrderPrice - filledPrice < 10)
                {
                    newPrice = filledPrice + 10;
                    allowMoving = true;
                }
                else
                {
                    #region Code cũ - Có thể sử dụng lại sau này
                    /*
                     * Old code cho trường hợp stop loss đã về ngang với giá vào lệnh (break even). 
                     * - Có 2x contracts, cắt x contract còn x contracts
                     * - Khi giá lên [Target_Half + 7] thì đưa stop loss lên Target_Half
                     */

                    /*
                    allowMoving = allowMoving || (filledPrice <= stopOrderPrice && stopOrderPrice < TargetPrice_Half && TargetPrice_Half + 7 < updatedPrice);

                    LocalPrint($"Điều kiện để chuyển stop lên target 1 - filledPrice: {filledPrice:N2} <= stopOrderPrice: {stopOrderPrice:N2} <= TargetPrice_Half {TargetPrice_Half:N2} --> Allow move: {allowMoving}");

                    // Giá lên 37 điểm thì di chuyển stop loss lên 30 điểm
                    if (allowMoving)
                    {
                        newPrice = TargetPrice_Half;                        
                    }
                    */
                    #endregion
                    
                    
                    // Nếu giá đã về break even và cây nến là XANH
                    if (filledPrice <= stopOrderPrice && CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m, null, null))
                    {
                        var bodyLength = Math.Abs(closePrice_5m - openPrice_5m);

                        if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                        {
                            LocalPrint($"Nến xanh có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                            CloseExistingOrders();
                        }
                        else if (bodyLength > 12)
                        {
                            var newStopLossBasedOnGreenCandle = StrategiesUtilities.RoundPrice(openPrice_5m + Math.Abs(closePrice_5m - openPrice_5m) / 3);

                            allowMoving = stopOrderPrice < newStopLossBasedOnGreenCandle;

                            if (allowMoving)
                            {
                                LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnGreenCandle:N2}");
                                newPrice = newStopLossBasedOnGreenCandle;
                            }
                        }
                    }
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
                else if (filledPrice - updatedPrice>= 60 && filledPrice - stopOrderPrice < 40)
                {
                    newPrice = filledPrice - 40;
                    allowMoving = true;
                }
                else if (filledPrice - updatedPrice >= 30 && filledPrice - stopOrderPrice < 10)
                {
                    newPrice = filledPrice - 10;
                    allowMoving = true;
                }
                else
                {
                    #region Code cũ - Có thể sử dụng lại sau này
                    /*
                     * Old code cho trường hợp stop loss đã về ngang với giá vào lệnh (break even). 
                     * - Có 2x contracts, cắt x contract còn x contracts
                     * - Khi giá lên [Target_Half + 7] thì đưa stop loss lên Target_Half
                     */

                    /*
                    allowMoving = allowMoving || (filledPrice >= stopOrderPrice && stopOrderPrice > TargetPrice_Half && TargetPrice_Half - 7 > updatedPrice);

                    LocalPrint($"Điều kiện để chuyển stop lên target 1  - filledPrice: {filledPrice:N2} >= stopOrderPrice: {stopOrderPrice:N2} > TargetPrice_Half {TargetPrice_Half:N2} --> Allow move: {allowMoving}");

                    if (allowMoving)
                    {
                        newPrice = TargetPrice_Half;
                    }
                    */
                    #endregion

                    #region Code mới - Dịch stop loss dựa trên cây nến đỏ gần nhất
                    
                    if (filledPrice >= stopOrderPrice && CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m, null, null))
                    {
                        var bodyLength = Math.Abs(closePrice_5m - openPrice_5m);


                        if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                        {
                            LocalPrint($"Nến xanh có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                            CloseExistingOrders();
                        }
                        else if (bodyLength > 12)
                        {
                            var newStopLossBasedOnRedCandle = StrategiesUtilities.RoundPrice(openPrice_5m - Math.Abs(closePrice_5m - openPrice_5m) / 3);

                            allowMoving = stopOrderPrice > newStopLossBasedOnRedCandle;

                            if (allowMoving)
                            {
                                LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnRedCandle:N2}");
                                newPrice = newStopLossBasedOnRedCandle;
                            }
                        }    
                    }
                    
                    #endregion
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
