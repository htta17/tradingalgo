﻿#region Using declarations
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

        public Rooster() : this("ROOSTER") { }

        private const int DEMA_Period = 9;

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
        protected double volumeBuy_5m = -1;
        protected double volumeSell_5m = -1;

        // RSI
        protected double rsi_5m = -1;

        // WAE Values 
        protected double waeDeadVal_5m = -1;
        protected double waeExplosion_5m = -1;
        protected double waeUptrend_5m = -1;
        protected double waeDowntrend_5m = -1;

        // Fish trend value 
        protected double middleEma4651_5m = -1;
        protected double ema46_5m = -1;
        protected double ema51_5m = -1;

        protected Series<WAE_ValueSet> waeValuesSeries_5m;        

        #region Indicators
        private Bollinger Bollinger1Indicator_5m { get; set; }
        private Bollinger Bollinger2Indicator_5m { get; set; }
        private WaddahAttarExplosion WAEIndicator_5m { get; set; }
        
        private RSI RSIIndicator_5m { get; set; }

        private WaddahAttarExplosion WAEIndicator_15m { get; set; }

        protected WAE_ValueSet wAE_ValueSet_15m { get; set; }

        //private MACD MACD_5m { get; set; }

        private EMA EMA46_5m { get; set; }
        private EMA EMA51_5m { get; set; }

        protected DateTime TouchEMA4651Time { get; set; } = DateTime.MinValue;

        // KeyLevels
        protected List<double> KeyLevels = new List<double>();        

        #endregion
        #endregion

        protected virtual bool ShouldCancelPendingOrdersByTrendCondition()
        {
            return
                // Trend suy yếu, 
                waeValuesSeries_5m[0].IsInDeadZone ||
                // Hiện tại có xu hướng bearish nhưng lệnh chờ là BUY
                (IsBuying && waeValuesSeries_5m[0].HasBEARVolume) ||
                // Hiện tại có xu hướng bullish nhưng lệnh chờ là SELL
                (IsSelling && waeValuesSeries_5m[0].HasBULLVolume);
        }

        protected override void UpdatePendingOrder()
        {
            if (TradingStatus != TradingStatus.PendingFill)
            {
                return;
            }
            
            // Cancel lệnh do đợi quá lâu
            var firstOrder = GetPendingOrder();

            if (firstOrder == null)
            {
                return;
            }

            var cancelOrderDueByTime = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
            if (cancelOrderDueByTime)
            {
                return;
            }

            /*
            if (ShouldCancelPendingOrdersByTrendCondition())
            {
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh do xu hướng hiện tại ngược với lệnh chờ");
                return;
            }
            */

            var checkShouldTradeAgain = ShouldTrade();

            if (checkShouldTradeAgain == TradeAction.NoTrade)
            {
                LocalPrint($"Check lại các điều kiện với [ShouldTrade], new answer: [{checkShouldTradeAgain}] --> Cancel lệnh do không thỏa mãn các điều kiện trade");
                CancelAllPendingOrder();
                return;
            }
            else 
            {
               
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(checkShouldTradeAgain);

                var newPrice = GetSetPrice(checkShouldTradeAgain, atmStrategy);

                var stopLossPrice = GetStopLossPrice(checkShouldTradeAgain, newPrice, atmStrategy);

                var targetPrice_Half = GetTargetPrice_Half(checkShouldTradeAgain, newPrice, atmStrategy);

                var targetPrice_Full = GetTargetPrice_Full(checkShouldTradeAgain, newPrice, atmStrategy);

                // Nếu ngược trend hoặc backtest thì vào cancel lệnh cũ và vào lệnh mới
                if (State == State.Historical || (CurrentTradeAction != checkShouldTradeAgain))
                {
                    #region Cancel current order and enter new one
                    CancelAllPendingOrder();

                    EnterOrder(checkShouldTradeAgain);
                    #endregion
                }
                // Ngược lại thì update điểm vào lệnh
                else if (State == State.Realtime)
                {
                    #region Begin of move pending order
                    UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);
                    #endregion
                }
            }
            //else
            //{
            //    LocalPrint($"[ShouldTrade], current: {CurrentTradeAction}, new: {checkShouldTradeAgain}, right now DO NOTHING.");
            //}
        }        

        /// <summary>
        /// Tìm các giá trị của Waddah Attar Explosion ở khung 5 phút
        /// </summary>
        /// <returns></returns>
        private WAE_ValueSet FindWaddahAttarExplosion()
        {
            return new WAE_ValueSet
            {
                DeadZoneVal = WAEIndicator_5m.Values[3][0],
                DownTrendVal = WAEIndicator_5m.Values[1][0],
                ExplosionVal = WAEIndicator_5m.Values[2][0],
                UpTrendVal = WAEIndicator_5m.Values[0][0]
            };
        }

        /// <summary>
        /// Tìm các giá trị của Waddah Attar Explosion ở khung 5 phút
        /// </summary>
        /// <returns></returns>
        private WAE_ValueSet FindWaddahAttarExplosion_15m()
        {
            return new WAE_ValueSet
            {
                DeadZoneVal = WAEIndicator_15m.Values[3][0],
                DownTrendVal = WAEIndicator_15m.Values[1][0],
                ExplosionVal = WAEIndicator_15m.Values[2][0],
                UpTrendVal = WAEIndicator_15m.Values[0][0]
            };
        }

        protected override TradeAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Trước 9:10am hoặc sau 2:00pm thì không nên trade 
            if (time < 091000 && time < 140000)
            {
                LocalPrint($"Rooster chỉ sử dụng từ 9:10a-2:00pm --> No Trade.");
                return TradeAction.NoTrade;
            }

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

                DrawKeyLevels("MiddleEMA", (ema51_1m + ema29_1m) / 2, Brushes.Gold, Brushes.Green); 
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

                rsi_5m = RSIIndicator_5m[0];

                volume_5m = Volume[0];

                /*
                adx_5m = ADX(FiveMinutes_Period).Value[0];
                plusDI_5m = DM(FiveMinutes_Period).DiPlus[0];
                minusDI_5m = DM(FiveMinutes_Period).DiMinus[0];
                */
                ema46_5m = EMA46_5m.Value[0];
                ema46_5m = EMA46_5m.Value[0];
                middleEma4651_5m = (EMA46_5m.Value[0] + EMA51_5m.Value[0]) / 2.0; 

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

                if ((lowPrice_5m < ema46_5m && highPrice_5m > ema46_5m) || (lowPrice_5m < ema51_5m && highPrice_5m > ema51_5m))
                {
                    TouchEMA4651Time = Time[0];
                    LocalPrint($"Touch EMA46/51 at {TouchEMA4651Time}");
                }    

                prev_lowPrice_5m = Low[1];
                prev_highPrice_5m = High[1];
                prev_closePrice_5m = Close[1];
                prev_openPrice_5m = Open[1];

                CurrentBarIndex_5m = CurrentBar;

                currentDEMA_5m = DEMA(DEMA_Period).Value[0];
                lastDEMA_5m = DEMA(DEMA_Period).Value[1];
                currentPrice = Close[0];

                var wae = FindWaddahAttarExplosion();

                waeValuesSeries_5m[0] = wae;

                waeDeadVal_5m = wae.DeadZoneVal;
                waeDowntrend_5m = wae.DownTrendVal;
                waeExplosion_5m = wae.ExplosionVal;
                waeUptrend_5m = wae.UpTrendVal;

                LocalPrint($"Current Status: {TradingStatus}, WAE Values: DeadZoneVal: {wae.DeadZoneVal:N2}, ExplosionVal: {wae.ExplosionVal:N2}, " +
                    $"DowntrendVal: {wae.DownTrendVal:N2}, " +
                    $"UptrendVal: {wae.UpTrendVal:N2}." +
                    $"{(wae.HasBULLVolume ? "--> BULL Volume" : wae.HasBEARVolume ? "--> BEAR Volume" : "")}");

                #region Enter order
                if (State != State.Realtime)
                {
                    return;
                }

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    // Điều kiện [barIndex_5m != enteredbarIndex_5m] để tránh việc trade 1 bar 5 phút nhiều lần
                    if (shouldTrade != TradeAction.NoTrade)// && CurrentBarIndex_5m != EnteredBarIndex_5m)
                    {
                        EnterOrder(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    UpdatePendingOrder();
                }
                #endregion
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 15)
            {
                wAE_ValueSet_15m = FindWaddahAttarExplosion_15m();
            }    
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Rooster (EMA46/51 ONLY)";
            Description = "[Rooster] là giải thuật trade ngược trend, dùng EMA46/51 only.";

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";
        }

        protected override bool IsBuying
        {
            get { return CurrentTradeAction == TradeAction.Buy_Trending || CurrentTradeAction == TradeAction.Buy_Reversal; }
        }

        protected override bool IsSelling
        {
            get { return CurrentTradeAction == TradeAction.Sell_Trending || CurrentTradeAction == TradeAction.Sell_Reversal; }
        }

        protected bool IsReverseTrade
        {
            get
            {
                return CurrentTradeAction == TradeAction.Sell_Reversal || CurrentTradeAction == TradeAction.Buy_Reversal;
            }
        }

        protected bool IsTrendingTrade
        {
            get
            {
                return CurrentTradeAction == TradeAction.Buy_Trending || CurrentTradeAction == TradeAction.Sell_Trending;
            }
        }

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 15);
            }    
            else if (State == State.DataLoaded )
            {
                Bollinger1Indicator_5m = Bollinger(1, 20);
                Bollinger1Indicator_5m.Plots[0].Brush = Bollinger1Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                Bollinger1Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                Bollinger2Indicator_5m = Bollinger(2, 20);
                Bollinger2Indicator_5m.Plots[0].Brush = Bollinger2Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                Bollinger2Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                waeValuesSeries_5m = new Series<WAE_ValueSet>(this);

                /* 
                 * Bars Array orders: 
                 * 0: Current view
                 * 1: 5 minutes (Check BarClosedATMBase [OnStateChange]) 
                 * 2: 1 minutes (Check BarClosedATMBase [OnStateChange]) 
                 * 3: 15 minutes
                 */

                WAEIndicator_5m = WaddahAttarExplosion(BarsArray[1]);
                WAEIndicator_15m = WaddahAttarExplosion(BarsArray[3]);

                RSIIndicator_5m = RSI(14, 3);

                //MACD_5m = MACD(12, 26, 9);

                EMA46_5m = EMA(46);
                EMA46_5m.Plots[0].Brush = Brushes.DarkOrange;

                EMA51_5m = EMA(51);
                EMA51_5m.Plots[0].Brush = Brushes.DeepSkyBlue;
                EMA51_5m.Plots[0].DashStyleHelper = DashStyleHelper.Dash;

                AddChartIndicator(Bollinger1Indicator_5m);
                AddChartIndicator(Bollinger2Indicator_5m);
                
                AddChartIndicator(WAEIndicator_5m);
                //AddChartIndicator(MACD_5m);
                AddChartIndicator(EMA46_5m);
                AddChartIndicator(EMA51_5m);
                AddChartIndicator(RSIIndicator_5m);
            }            
        }

        /// <summary>
        /// Tìm giá để set dựa theo EMA29/51 hoặc dựa theo Bollinger bands
        /// </summary>        
        /// <param name="tradeAction">NoTrade, Sell_Reversal, Buy_Reversal, Sell_Trending, Buy_Trending</param>
        /// <returns></returns>
        protected override double GetSetPrice(TradeAction tradeAction, AtmStrategy atmStrategy)
        {
            return StrategiesUtilities.RoundPrice(middleEma4651_5m);            
        }
        /*
         * End of class 
         */
    }
}
