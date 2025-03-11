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
using Rules1;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public class Rooster : BarClosedATMBase<TradeAction>, IATMStrategy
    {
        public Rooster(string name) : base(name) { }

        public Rooster() : this("ROOSTER_ATM") { }

        private const int DEMA_Period = 9;
        private const int FiveMinutes_Period = 14;

        #region 1 minute values
        protected double ema21_1m = -1;
        protected double ema29_1m = -1;
        protected double ema51_1m = -1;
        protected double ema120_1m = -1;
        protected double currentPrice = -1;
        #endregion

        #region 5 minutes values 
        protected double upperBB_5m = -1;
        protected double lowerBB_5m = -1;
        protected double middleBB_5m = -1;

        protected double lastUpperBB_5m = -1;
        protected double lastLowerBB_5m = -1;

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;
        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double prev_lowPrice_5m = -1;
        protected double prev_highPrice_5m = -1;
        protected double prev_closePrice_5m = -1;
        protected double prev_openPrice_5m = -1;

        protected double currentDEMA_5m = -1;
        protected double lastDEMA_5m = -1;

        // Volume 
        protected double volume_5m = -1;
        protected double avgEMAVolume_5m = -1;
        protected double volumeBuy_5m = -1;
        protected double volumeSell_5m = -1;

        // RSI
        protected double rsi_5m = -1;

        // WAE Values 
        protected double waeDeadVal_5m = -1;
        protected double waeExplosion_5m = -1;
        protected double waeUptrend_5m = -1;
        protected double waeDowntrend_5m = -1;

        protected Series<double> deadZoneSeries;
        protected Series<WAE_ValueSet> waeValuesSeries;

        private Bollinger bollinger1Indicator_5m { get; set; }
        private Bollinger bollinger2Indicator_5m { get; set; }
        #endregion

        protected virtual bool ShouldCancelPendingOrdersByTrendCondition()
        {
            return
                // Trend suy yếu, 
                waeValuesSeries[0].IsInDeadZone ||
                // Hiện tại có xu hướng bearish nhưng lệnh chờ là BUY
                (IsBuying && waeValuesSeries[0].HasBEARVolume) ||
                // Hiện tại có xu hướng bullish nhưng lệnh chờ là SELL
                (IsSelling && waeValuesSeries[0].HasBULLVolume);
        }

        protected override void UpdatePendingOrder()
        {
            if (TradingStatus != TradingStatus.PendingFill)
            {
                return;
            }
            
            // Cancel lệnh do đợi quá lâu
            var firstOrder = GetOrderFromPendingList();

            if (firstOrder == null)
            {
                return;
            }

            var cancelOrderDueByTime = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
            if (cancelOrderDueByTime)
            {
                return;
            }

            if (ShouldCancelPendingOrdersByTrendCondition())
            {
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh do xu hướng hiện tại ngược với lệnh chờ");
                return;
            }

            #region Begin of move pending order
            var newPrice = GetSetPrice(CurrentTradeAction);

            var stopLossPrice = GetStopLossPrice(CurrentTradeAction, newPrice);

            var targetPrice_Half = GetTargetPrice_Half(CurrentTradeAction, newPrice);

            var targetPrice_Full = GetTargetPrice_Full(CurrentTradeAction, newPrice);

            if (State == State.Historical)
            {
                CancelAllPendingOrder();

                EnterOrder(CurrentTradeAction);
            }
            else if (State == State.Realtime)
            {
                UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full);
            }
            #endregion
        }

        /// <summary>
        /// Tìm các giá trị của Waddah Attar Explosion ở khung 5 phút
        /// </summary>
        /// <returns></returns>
        private WAE_ValueSet FindWaddahAttarExplosion()
        {
            int sensitivity = 150;
            int fastLength = 20;
            int slowLength = 40;
            int channelLength = 20;
            double mult = 2.0;

            // WAE
            // Calculate Typical Price
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3.0;

            // Calculate True Range and store it in a Series
            double trueRange = Math.Max(High[0] - Low[0], Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
            deadZoneSeries[0] = trueRange; // Initialize the first value

            // Calculate smoothed ATR using EMA of the True Range Series
            double smoothedATR = EMA(deadZoneSeries, 100)[0];

            // Dead Zone
            double deadZone = smoothedATR * 3.7;

            // MACD Difference Calculation
            double fastEMA = EMA(Close, fastLength)[0];
            double slowEMA = EMA(Close, slowLength)[0];
            double prevFastEMA = EMA(Close, fastLength)[1];
            double prevSlowEMA = EMA(Close, slowLength)[1];

            double macd = fastEMA - slowEMA;
            double prevMacd = prevFastEMA - prevSlowEMA;
            double trendCalculation = (macd - prevMacd) * sensitivity;

            // Bollinger Bands Calculation
            double bbBasis = SMA(Close, channelLength)[0];
            double bbDev = mult * StdDev(Close, channelLength)[0];
            double bbUpperVal = bbBasis + bbDev;
            double bbLowerVal = bbBasis - bbDev;

            // Explosion Line
            double explosionValue = bbUpperVal - bbLowerVal;

            return new WAE_ValueSet
            {
                DeadZoneVal = deadZone,
                DownTrendVal = trendCalculation < 0 ? -trendCalculation : 0,
                ExplosionVal = explosionValue,
                UpTrendVal = trendCalculation >= 0 ? trendCalculation : 0
            };
        }

        protected override TradeAction ShouldTrade()
        {
            var currentWAE = waeValuesSeries[0];
            var previousWAE = waeValuesSeries[1];

            // Có 2 cây nến kề nhau có cùng volume
            if (waeValuesSeries[0].HasBEARVolume && waeValuesSeries[1].DownTrendVal > 0)
            {
                FilledTime = Time[0];

                // Volume tăng dần: Mua theo 1/2, 1/3 hoặc 1/4 cây nến trước. 

                // Volume giảm dần: Mua theo Bollinger band, ema29/51

                return TradeAction.Sell_Trending;
            }
            else if (waeValuesSeries[0].HasBULLVolume && waeValuesSeries[1].DownTrendVal > 0)
            {
                FilledTime = Time[0];

                // Volume tăng dần: Mua theo 1/2, 1/3 hoặc 1/4 cây nến trước. 

                // Volume giảm dần: Mua theo Bollinger band, ema29/51

                return TradeAction.Buy_Trending;
            }

            return TradeAction.NoTrade;
        }

        protected override void OnBarUpdate()
        {
            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();            

            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            base.OnBarUpdate();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                // Cập nhật EMA29 và EMA51	
                ema21_1m = EMA(21).Value[0];
                ema29_1m = EMA(29).Value[0];
                ema51_1m = EMA(51).Value[0];
                ema120_1m = EMA(120).Value[0];

                currentPrice = Close[0];

                if (State != State.Realtime)
                {
                    return;
                }

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    // Điều kiện [barIndex_5m != enteredbarIndex_5m] để tránh việc trade 1 bar 5 phút nhiều lần
                    if (shouldTrade != TradeAction.NoTrade && CurrentBarIndex_5m != EnteredBarIndex_5m)
                    {
                        EnterOrder(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    UpdatePendingOrder();
                }

            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                if (BarsInProgress == 0)
                {
                    // Current View --> return
                    return;
                }

                var bollinger = Bollinger(1, 20);
                var bollingerStd2 = Bollinger(2, 20);

                rsi_5m = RSI(14, 3)[0];

                volume_5m = Volume[0];
                avgEMAVolume_5m = EMA(Volume, FiveMinutes_Period)[0];

                /*
                adx_5m = ADX(FiveMinutes_Period).Value[0];
                plusDI_5m = DM(FiveMinutes_Period).DiPlus[0];
                minusDI_5m = DM(FiveMinutes_Period).DiMinus[0];
                */

                upperBB_5m = bollinger.Upper[0];
                lowerBB_5m = bollinger.Lower[0];
                middleBB_5m = bollinger.Middle[0];

                lastUpperBB_5m = bollinger.Upper[1];
                lastLowerBB_5m = bollinger.Lower[1];

                upperStd2BB_5m = bollingerStd2.Upper[0];
                lowerStd2BB_5m = bollingerStd2.Lower[0];

                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                closePrice_5m = Close[0];
                openPrice_5m = Open[0];

                prev_lowPrice_5m = Low[1];
                prev_highPrice_5m = High[1];
                prev_closePrice_5m = Close[1];
                prev_openPrice_5m = Open[1];

                CurrentBarIndex_5m = CurrentBar;

                currentDEMA_5m = DEMA(DEMA_Period).Value[0];
                lastDEMA_5m = DEMA(DEMA_Period).Value[1];
                currentPrice = Close[0];

                var wae = FindWaddahAttarExplosion();

                waeValuesSeries[0] = wae;

                waeDeadVal_5m = wae.DeadZoneVal;
                waeDowntrend_5m = wae.DownTrendVal;
                waeExplosion_5m = wae.ExplosionVal;
                waeUptrend_5m = wae.UpTrendVal;

                LocalPrint($"Current Status: {TradingStatus}, WAE Values: DeadZoneVal: {wae.DeadZoneVal:N2}, ExplosionVal: {wae.ExplosionVal:N2}, " +
                    $"DowntrendVal: {wae.DownTrendVal:N2}, " +
                    $"UptrendVal: {wae.UpTrendVal:N2}." +
                    $"{(wae.HasBULLVolume ? "--> BULL Volume" : wae.HasBEARVolume ? "--> BEAR Volume" : "")}");
            }
        }
        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Rooster ATM (Chicken with Trending ONLY)";
            Description = "[Rooster ATM] là giải thuật [Chicken] nhưng chỉ chạy Trending, dùng ATM Strategy để vào lệnh";

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";

            SetBreakEvenManually = false;
        }

        protected override bool IsBuying
        {
            get { return CurrentTradeAction == TradeAction.Buy_Trending; }
        }

        protected override bool IsSelling
        {
            get { return CurrentTradeAction == TradeAction.Sell_Trending; }
        }

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                FullSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(FullSizeATMName).AtmStrategy;

                HalfSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(HalfSizefATMName).AtmStrategy;
            }
            else if (State == State.DataLoaded )
            {
                bollinger1Indicator_5m = Bollinger(1, 20);
                bollinger1Indicator_5m.Plots[0].Brush = bollinger1Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                bollinger2Indicator_5m = Bollinger(2, 20);
                bollinger2Indicator_5m.Plots[0].Brush = bollinger2Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                deadZoneSeries = new Series<double>(this);
                waeValuesSeries = new Series<WAE_ValueSet>(this);

                AddChartIndicator(bollinger1Indicator_5m);
                AddChartIndicator(bollinger2Indicator_5m);
            }
            else if (State == State.Realtime)
            {
                // Load thông tin liên quan đến
                if (File.Exists(FileName))
                {
                    try
                    {
                        var text = File.ReadAllText(FileName);

                        var arr = text.Split(',');

                        if (arr.Length == 1)
                        {
                            AtmStrategyId = arr[0];
                        }
                        else if (arr.Length == 2)
                        {
                            AtmStrategyId = arr[0];
                            OrderId = arr[1];

                            tradingStatus = CheckCurrentStatusBasedOnOrders();
                            LocalPrint($"Initial status - {tradingStatus}");
                        }
                    }
                    catch (Exception e)
                    {
                        Print(e.Message);
                    }
                }
            }
        }

        // <summary>
        /// Tìm giá để set dựa theo EMA29/51 hoặc dựa theo Bollinger bands
        /// </summary>        
        /// <param name="tradeAction">NoTrade, Sell_Reversal, Buy_Reversal, Sell_Trending, Buy_Trending</param>
        /// <returns></returns>
        protected override double GetSetPrice(TradeAction tradeAction)
        {
            var middleEMA = (ema29_1m + ema51_1m) / 2.0;
           
            // Nếu volume đang yếu hoặc Medium thì 
            var volumeStrength = waeValuesSeries[0].WAE_Strength;
            LocalPrint($"Volume Strength: SUM: {(waeValuesSeries[0].DownTrendVal + waeValuesSeries[0].UpTrendVal):N2}, [{volumeStrength.ToString()}]");
            if (volumeStrength == WAE_Strength.Weak || volumeStrength == WAE_Strength.Medium)
            {
                return StrategiesUtilities.RoundPrice(middleEMA);
            }
            else if (volumeStrength == WAE_Strength.Strong || volumeStrength == WAE_Strength.SuperStrong)
            {
                var currentCandleIs_RED = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m);

                var currentCandleIs_GREEN = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m);

                LocalPrint($"Current Red: {currentCandleIs_RED}, Current GREEN: {currentCandleIs_GREEN}, tradeAction: {tradeAction}");

                // Tìm điểm vào lệnh thích hợp. 
                // Nếu cây nến hiện tại cùng chiều market (Red khi bearish, hoặc Green khi bullish) 
                var wholeBody = Math.Abs(closePrice_5m - openPrice_5m);
                // Hệ số (so với cây nến trước): Lấy 1/2 nếu Strong, 1/3 nếu Super Strong
                var coeff = volumeStrength == WAE_Strength.Strong ? 2.0 : 3.0;

                if (tradeAction == TradeAction.Buy_Trending && currentCandleIs_GREEN)
                {
                    // Đặt lệnh BUY với 1/3 cây nến trước đó 
                    return StrategiesUtilities.RoundPrice(closePrice_5m - (wholeBody / coeff));
                }
                else if (tradeAction == TradeAction.Sell_Trending && currentCandleIs_RED)
                {
                    // Đặt lệnh SELL với 1/3 cây nến trước đó 
                    return StrategiesUtilities.RoundPrice(closePrice_5m + (wholeBody / coeff));
                }
            }      

            // Khó quá cứ lấy EMA29/51
            return StrategiesUtilities.RoundPrice(middleEMA);
        }


        /// <summary>
        /// Giá cho target 1 (Half)
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
        protected override double GetTargetPrice_Half(TradeAction tradeAction, double setPrice)
        {
            double price = -1;

            var time = ToTime(Time[0]);
            var isNightTime = time > 150000 || time < 083000;

            switch (tradeAction)
            {
                case TradeAction.Buy_Trending:
                    price = isNightTime // if night time, cut half at 7 point
                        ? setPrice + 5
                        : setPrice + (TickSize * Target1InTicks);
                    break;

                case TradeAction.Sell_Trending:
                    price = isNightTime // if night time, cut half at 7 point
                        ? setPrice - 5
                        : setPrice - (TickSize * Target1InTicks);
                    break;
            }

            return StrategiesUtilities.RoundPrice(price);
        }

        /// <summary>
        /// Giá cho target 2 (Full)
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
        protected override double GetTargetPrice_Full(TradeAction tradeAction, double setPrice)
        {
            double price = -1;

            switch (tradeAction)
            {
                case TradeAction.Buy_Trending:
                    price = setPrice + TickSize * Target2InTicks;
                    break;

                case TradeAction.Sell_Trending:
                    price = setPrice - TickSize * Target2InTicks;
                    break;
            }

            return StrategiesUtilities.RoundPrice(price);
        }
        protected override double GetStopLossPrice(TradeAction tradeAction, double setPrice)
        {
            var profitOrLoss = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var atmStrategy = profitOrLoss >= -ReduceSizeIfProfit ? FullSizeAtmStrategy : HalfSizeAtmStrategy;

            // Get stop loss and target ID based on strategy 
            var stopLossTick = atmStrategy.Brackets[0].StopLoss;
            var stopLossPrice = IsBuying ?
                setPrice - stopLossTick * TickSize :
                setPrice + stopLossTick * TickSize;

            return stopLossPrice;
        }

    }

}
