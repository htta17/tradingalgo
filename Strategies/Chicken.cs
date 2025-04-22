//#define ENABLE_ADX_DI

#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Custom.Strategies;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Media;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public abstract class Chicken : BarClosedBaseClass<TradeAction, TradeAction>
    {
        public Chicken() : this("CHICKEN")
        {
            HalfPriceSignals = new HashSet<string>
            {
                StrategiesUtilities.SignalEntry_ReversalHalf,
                StrategiesUtilities.SignalEntry_TrendingHalf
            };

            EntrySignals = new HashSet<string>
            {
                StrategiesUtilities.SignalEntry_ReversalHalf,
                StrategiesUtilities.SignalEntry_TrendingHalf,
                StrategiesUtilities.SignalEntry_TrendingFull,
                StrategiesUtilities.SignalEntry_ReversalHalf,
            };
        }

        public Chicken(string name) : base(name)
        {
            
        }
        
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

        /*
        // ADX
        protected double adx_5m = -1;
        protected double plusDI_5m = -1;
        protected double minusDI_5m = -1;
        */

        // WAE Values 
        protected double waeDeadVal_5m = -1;
        protected double waeExplosion_5m = -1;
        protected double waeUptrend_5m = -1;
        protected double waeDowntrend_5m = -1;

        protected Series<double> deadZoneSeries;
        protected Series<WAE_ValueSet> waeValuesSeries;
        #endregion

        /// <summary>
        /// Lệnh hiện tại là lệnh mua
        /// </summary>
        protected override bool IsBuying
        {
            get
            {
                return CurrentTradeAction == TradeAction.Buy_Reversal || CurrentTradeAction == TradeAction.Buy_Trending;
            }
        }

        /// <summary>
        /// Lệnh hiện tại là lệnh bán 
        /// </summary>
        protected override bool IsSelling
        {
            get
            {
                return CurrentTradeAction == TradeAction.Sell_Reversal || CurrentTradeAction == TradeAction.Sell_Trending;
            }
        }

        private bool IsReverseTrade
        {
            get
            {
                return CurrentTradeAction == TradeAction.Sell_Reversal || CurrentTradeAction == TradeAction.Buy_Reversal;
            }
        }

        private bool IsTrendingTrade
        {
            get
            {
                return CurrentTradeAction == TradeAction.Buy_Trending || CurrentTradeAction == TradeAction.Sell_Trending;
            }
        }

        /// <summary>
        /// Cách thức đặt lệnh trade (Cho trending)
        /// </summary>
        protected TrendPlaceToSetOrder TrendPlaceToSetOrder { get; set; }

        private const string Configuration_ChickkenParams_Name = "Chicken parameters";

        #region Importants Configurations
        /// <summary>
        /// If gain is more than [StopWhenGain], stop trading for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lượng contract: ",
            Order = 6,
            GroupName = StrategiesUtilities.Configuration_DailyPnL_Name)]
        public int NumberOfContract { get; set; }

        /// <summary>
        /// Điểm vào lệnh (Theo EMA29/51 hay Bollinger band)
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enter order price:",
            Order = 3,
            GroupName = Configuration_ChickkenParams_Name)]
        public ReversePlaceToSetOrder ReversePlaceToSetOrder { get; set; } = ReversePlaceToSetOrder.BollingerBand;

        /// <summary>
        /// Cho phép trade theo trending
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Trending Trade?", Order = 1, GroupName = Configuration_ChickkenParams_Name)]
        public bool AllowTrendingTrade { get; set; }

        /// <summary>
        /// Cho phép trade theo trending
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reversal Trade?", Order = 2, GroupName = Configuration_ChickkenParams_Name)]
        public bool AllowReversalTrade { get; set; }

        /// <summary>
        /// - Nếu đang lỗ (&lt; $100) hoặc đang lời thì vào 2 contracts <br/>
        /// - Nếu đang lỗ > $100 thì vào 1 contract
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduce number of contract when profit less than (< 0):", Order = 2, GroupName = Configuration_ChickkenParams_Name)]
        public int ReduceSizeIfProfit { get; set; }

        protected virtual bool InternalAllowTrendingTrade
        {
            get 
            {
                return AllowTrendingTrade;
            }
        }

        protected virtual bool InternalAllowReversalTrade
        {
            get
            {
                return AllowReversalTrade;
            }
        }

        #endregion

        /// <summary>
        /// Đưa hết các properties vào 1 nơi
        /// </summary>
        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();
            // General properties
            Description = @"Play on 5 minutes frame.";
            Name = this.Name;
            BarsRequiredToTrade = 20;

            SetOrderQuantity = SetOrderQuantity.Strategy;
            DefaultQuantity = 2;

            // Stop loss/Target profit properties
            Target1InTicks = 40;

            ReduceSizeIfProfit = 100;

            // Chicken 
            ReversePlaceToSetOrder = ReversePlaceToSetOrder.BollingerBand;
            AllowTrendingTrade = true;
            AllowReversalTrade = true;

            NumberOfContract = 1;
        }

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
            else if (State == State.DataLoaded)
            {
                var bollinger1 = Bollinger(1, 20);
                bollinger1.Plots[0].Brush = bollinger1.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1.Plots[1].Brush = Brushes.DeepPink;

                var bollinger2 = Bollinger(2, 20);
                bollinger2.Plots[0].Brush = bollinger2.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2.Plots[1].Brush = Brushes.DeepPink;
                
                AddChartIndicator(bollinger1);
                AddChartIndicator(bollinger2);
                AddChartIndicator(DEMA(9));                

                deadZoneSeries = new Series<double>(this);
                waeValuesSeries = new Series<WAE_ValueSet>(this);
            }            
        }

        /// <summary>
        /// Kiểm tra điều kiện để vào lệnh
        /// </summary>
        /// <returns>Trade Action: Sell/Buy, Trending/Reverse</returns>
        protected override TradeAction ShouldTrade()
        {
            /*
			* Điều kiện để trade (SHORT) 
			* 1. currentDEMA < upper & lastDEMA >= upper
			* 2. currentPrice > lower && currentPrice < upper
			*/
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return TradeAction.NoTrade;
            }

            // Configure cho phép trade reversal 
            if (InternalAllowReversalTrade)
            {
                // Cho phép trade reverse (Bollinger Band) từ 8:35 am đến 11:30pm
                if (currentPrice > lowerBB_5m && currentPrice < upperBB_5m)
                {
                    if (lastDEMA_5m > lastUpperBB_5m && currentDEMA_5m <= upperBB_5m)
                    {
                        LocalPrint("Found SELL signal (Reversal)");

                        FilledTime = Time[0];

                        return TradeAction.Sell_Reversal;
                    }
                    else if (lastDEMA_5m < lastLowerBB_5m && currentDEMA_5m >= lowerBB_5m)
                    {
                        LocalPrint("Found BUY signal (Reversal)");

                        FilledTime = Time[0];

                        return TradeAction.Buy_Reversal;
                    }
                }
            }

            if (InternalAllowTrendingTrade)
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
            }

            return TradeAction.NoTrade;
        }

        /// <summary>
        /// Tìm giá để set dựa theo EMA29/51 hoặc dựa theo Bollinger bands
        /// </summary>        
        /// <param name="tradeAction">NoTrade, Sell_Reversal, Buy_Reversal, Sell_Trending, Buy_Trending</param>
        /// <returns></returns>
        protected override double GetSetPrice(TradeAction tradeAction, TradeAction action)
        {
            var middleEMA = (ema29_1m + ema51_1m) / 2.0;

            switch (tradeAction)
            {
                /*
                 * TRENDING 
                 */
                case TradeAction.Buy_Trending:
                case TradeAction.Sell_Trending:
                    {
                        // Nếu volume đang yếu hoặc Medium thì 
                        var volumeStrength = waeValuesSeries[0].WAE_Strength;
                        LocalPrint($"Volume Strength: SUM: {(waeValuesSeries[0].DownTrendVal + waeValuesSeries[0].UpTrendVal):N2}, [{ volumeStrength.ToString() }]");
                        if (volumeStrength == WAE_Strength.SuperWeak || volumeStrength == WAE_Strength.Weak)
                        {
                            return StrategiesUtilities.RoundPrice(middleEMA);
                        }
                        else if (volumeStrength == WAE_Strength.Medium || volumeStrength == WAE_Strength.MediumStrong)
                        {
                            var currentCandleIs_RED = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m);

                            var currentCandleIs_GREEN = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m);

                            LocalPrint($"Current Red: {currentCandleIs_RED}, Current GREEN: {currentCandleIs_GREEN}, tradeAction: {tradeAction}");

                            // Tìm điểm vào lệnh thích hợp. 
                            // Nếu cây nến hiện tại cùng chiều market (Red khi bearish, hoặc Green khi bullish) 
                            var wholeBody = Math.Abs(closePrice_5m - openPrice_5m);
                            // Hệ số (so với cây nến trước): Lấy 1/2 nếu Strong, 1/3 nếu Super Strong
                            var coeff = volumeStrength == WAE_Strength.Medium ? 2.0 : 3.0;

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
                    }
                    break;
                /*
                 * REVERSAL
                 */
                case TradeAction.Sell_Reversal:
                    return StrategiesUtilities.RoundPrice(ReversePlaceToSetOrder == ReversePlaceToSetOrder.EMA2951 ? middleEMA : upperBB_5m);

                case TradeAction.Buy_Reversal:
                    return StrategiesUtilities.RoundPrice(ReversePlaceToSetOrder == ReversePlaceToSetOrder.EMA2951 ? middleEMA : lowerBB_5m);
                    
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
        protected override double GetTargetPrice_Half(TradeAction tradeAction, double setPrice, TradeAction action)
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

                case TradeAction.Buy_Reversal:
                    price = middleBB_5m;
                    break;

                case TradeAction.Sell_Reversal:
                    price = middleBB_5m;
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
        protected override double GetTargetPrice_Full(TradeAction tradeAction, double setPrice, TradeAction action)
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

                case TradeAction.Buy_Reversal:
                    price = upperBB_5m;
                    break;

                case TradeAction.Sell_Reversal:
                    price = lowerBB_5m;
                    break;
            }

            return StrategiesUtilities.RoundPrice(price);
        }

        protected override double GetStopLossPrice(TradeAction tradeAction, double setPrice, TradeAction action)
        {
            double price = -1;

            switch (tradeAction)
            {
                #region Chicken stop loss price 
                case TradeAction.Buy_Trending:
                    price = setPrice - TickSize * StopLossInTicks;
                    break;

                case TradeAction.Sell_Trending:
                    price = setPrice + TickSize * StopLossInTicks;
                    break;

                case TradeAction.Buy_Reversal:
                    price = setPrice - TickSize * StopLossInTicks;
                    break;

                case TradeAction.Sell_Reversal:
                    price = setPrice + TickSize * StopLossInTicks;
                    break;
                    #endregion
            }

            return StrategiesUtilities.RoundPrice(price);
        }

        /// <summary>
        /// Đặt lệnh mua/bán
        /// </summary>
        /// <param name="tradeAction"></param>
        protected override void EnterOrder(TradeAction tradeAction)
        {
            // Set global values
            CurrentTradeAction = tradeAction;

            EnteredBarIndex_5m = CurrentBarIndex_5m;

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            double priceToSet = GetSetPrice(tradeAction, tradeAction);
            FilledPrice = priceToSet;            

            var stopLossPrice = GetStopLossPrice(CurrentTradeAction, priceToSet, tradeAction);

            LocalPrint($"Enter {action} at {Time[0]}, price to set: {priceToSet:N2}");

            var pnl = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
            var quantity = NumberOfContract;

            if (pnl >= -ReduceSizeIfProfit)
            {
                quantity = quantity * 2;
            }

            try
            {
                var signalHalf = IsTrendingTrade ? StrategiesUtilities.SignalEntry_TrendingHalf : StrategiesUtilities.SignalEntry_ReversalHalf;
                EnterOrderPure(priceToSet, Target1InTicks, StopLossInTicks, signalHalf, quantity, IsBuying, IsSelling);

                var signalFull = IsTrendingTrade ? StrategiesUtilities.SignalEntry_TrendingFull : StrategiesUtilities.SignalEntry_ReversalFull;
                EnterOrderPure(priceToSet, Target2InTicks, StopLossInTicks, signalFull, quantity, IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        /// <summary>
        /// Move half price target dựa trên giá Bollinger Band Middle
        /// </summary>
        private void MoveTargetsBasedOnBollinger()
        {
            // Hàm này chỉ dùng cho đánh ngược trend, nếu trending thì out.
            if (IsTrendingTrade)
            {
                return; 
            }
            // Move target 1
            var targetHalfPriceOrders = ActiveOrders.Values.ToList().Where(c => c.FromEntrySignal == StrategiesUtilities.SignalEntry_ReversalHalf &&
                (c.OrderType == OrderType.StopMarket || c.OrderType == OrderType.StopLimit)).ToList();

            var len = targetHalfPriceOrders.Count;

            for (var i = 0; i < len; i++)
            {
                var order = targetHalfPriceOrders[i];
                if ((IsBuying && middleBB_5m > FilledPrice) || (IsSelling && middleBB_5m < FilledPrice))
                {
                    MoveTargetOrStopOrder(middleBB_5m, order, true, IsBuying ? "BUY" : "SELL", order.FromEntrySignal);
                }
            }

            // Move target 2            
            var targetFullPriceOrders = ActiveOrders.Values.ToList().Where(c => c.FromEntrySignal == StrategiesUtilities.SignalEntry_ReversalFull &&
                (c.OrderType == OrderType.StopMarket || c.OrderType == OrderType.StopLimit)).ToList();

            var lenFull = targetHalfPriceOrders.Count;

            for (var i = 0; i < lenFull; i++)
            {
                var order = targetFullPriceOrders[i];
                var newFullPrice = GetTargetPrice_Full(CurrentTradeAction, FilledPrice, CurrentTradeAction);

                if ((IsBuying && newFullPrice > FilledPrice) || (IsSelling && newFullPrice < FilledPrice))
                {
                    MoveTargetOrStopOrder(newFullPrice, order, true, IsBuying ? "BUY" : "SELL", order.FromEntrySignal);
                }
            }
        }

        protected override void OnBarUpdate()
        {
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

                DrawKeyLevels("MiddleEMA",(ema29_1m + ema51_1m) / 2, Brushes.Gold, Brushes.Green);

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
                else if (TradingStatus == TradingStatus.OrderExists)
                {   
                    MoveTargetsBasedOnBollinger();
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

        /// <summary>
        /// Cập nhật giá trị cho các lệnh đang chờ, hoặc cancel do: Đợi lệnh quá 1h đồng hồ, do hết giờ trade, hoặc do 1 số điều kiện khác
        /// </summary>
        protected override void UpdatePendingOrder()
        {
            if (TradingStatus != TradingStatus.PendingFill)
            {
                return;
            }

            #region Cancel lệnh nếu có 1 trong các điều kiện:
            // Cancel lệnh do đợi quá lâu
            var firstOrder = GetPendingOrder();

            if (firstOrder == null)
            {
                return;
            }

            var cancelOrderDueByTime = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
            if (cancelOrderDueByTime)
            {
                CancelAllPendingOrder();
                return;
            }

            // Cancel cho lệnh theo đánh theo Bollinger (Ngược trend) 
            if (IsReverseTrade)
            {
                // Cancel khi cây nến đã vượt qua BB đường số 2 
                var cancelCausedByPrice = (firstOrder.IsLong && (highPrice_5m > upperStd2BB_5m || currentDEMA_5m > upperBB_5m))
                    || (firstOrder.IsShort && (lowPrice_5m < lowerStd2BB_5m || currentDEMA_5m < lowerBB_5m));
                if (cancelCausedByPrice)
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel lệnh do đã chạm Bollinger upper band (over bought) hoặc Bollinger lower band (over sold)");
                    return;
                }

                // Cancel nếu có 1 nguyên cây nến 5 phút vượt qua đường BB middle 
                var wholeCandlePassMiddleBand = (firstOrder.IsLong && lowPrice_5m > middleBB_5m) ||
                    (firstOrder.IsShort && highPrice_5m < middleBB_5m);
                if (wholeCandlePassMiddleBand)
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel vì đã có 1 cây nến 5 phút vượt qua được middle BB");
                    return;
                }
            }

            // Cancel các lệnh theo trending
            if (IsTrendingTrade)
            {
                if (ShouldCancelPendingOrdersByTrendCondition())
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel lệnh do xu hướng hiện tại ngược với lệnh chờ");
                    return;
                }
            }
            #endregion

            #region Begin of move pending order
            var newPrice = GetSetPrice(CurrentTradeAction, CurrentTradeAction);

            var stopLossPrice = GetStopLossPrice(CurrentTradeAction, newPrice, CurrentTradeAction);

            var targetPrice_Half = GetTargetPrice_Half(CurrentTradeAction, newPrice, CurrentTradeAction);

            var targetPrice_Full = GetTargetPrice_Full(CurrentTradeAction, newPrice, CurrentTradeAction);

            if (State == State.Historical)
            {
                CancelAllPendingOrder();

                EnterOrder(CurrentTradeAction);
            }
            else if (State == State.Realtime)
            {
                UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);
            }
            #endregion

        }

        protected override void UpdatePendingOrderPure(double newPrice, double stopLossPrice, double targetFull, double targetHalf)
        {
            if (Math.Abs(FilledPrice - newPrice) > 0.5)
            {
                FilledPrice = newPrice;
                
                var clonedList = ActiveOrders.Values.ToList();
                var len = clonedList.Count;

                for (var i = 0; i < len; i++)
                {
                    var order = clonedList[i];
                    try
                    {
                        LocalPrint($"Trying to modify waiting order [{order.Name}], " +
                            $"current Price: {order.LimitPrice}, current stop: {order.StopPrice}, " +
                            $"new Price: {newPrice:N2}, new stop loss: {stopLossPrice}, newTarget: {targetFull:N2}");

                        ChangeOrder(order, order.Quantity, newPrice, 0);
                    }
                    catch (Exception ex)
                    {
                        LocalPrint($"[UpdatePendingOrder] - ERROR: {ex.Message}");
                    }
                }
            }
        }

        protected override bool IsHalfPriceOrder(Order order)
        {
            return order.Name == StrategiesUtilities.SignalEntry_ReversalHalf || order.Name == StrategiesUtilities.SignalEntry_TrendingHalf;
        }

        protected override bool IsFullPriceOrder(Order order)
        {
            return order.Name == StrategiesUtilities.SignalEntry_ReversalFull || order.Name == StrategiesUtilities.SignalEntry_TrendingFull;
        }

        /*
		This should be blank to easy to see the function
		*/
    }
}