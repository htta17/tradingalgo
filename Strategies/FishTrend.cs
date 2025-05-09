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
using NinjaTrader.NinjaScript.SuperDomColumns;
using System.Xml.Linq;
using NinjaTrader.Gui.NinjaScript.Wizard;
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class FishTrend : BarClosedATMBase<FishTrendTradeDetail>
	{
        public FishTrend() : base("FISHTREND")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyFishTrend.txt");

            Configured_TimeFrameToTrade = TimeFrameToTrade.OneMinute;
        }
        protected override TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        protected TimeFrameToTrade Configured_TimeFrameToTrade { get; set; }

        protected override bool IsBuying => CurrentTradeAction.Action == GeneralTradeAction.Buy;

        protected override bool IsSelling => CurrentTradeAction.Action == GeneralTradeAction.Sell;      

        private double KeyLevel_5m_HIGH = -1;
        private double KeyLevel_5m_LOW = -1;

        private EMA EMA46Indicator_5m { get; set; }
        private EMA EMA50Indicator_5m { get; set; }

        private GeneralEMAsPosition Position_5m { get; set; }

        /// <summary>
        /// Số điểm cộng (hoặc trừ) so với đường EMA21 để vào lệnh MUA (hoặc BÁN).
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Adjustment Point:",
            Description = "Số điểm cộng (hoặc trừ) so với HIGH (hoặc LOW) khi MUA (hoặc BÁN).",
            Order = 2, GroupName = StrategiesUtilities.Configuration_Entry)]
        public int AdjustmentPoint { get; set; }

        private int Last5mBarTouchEMA50 { get; set; } = -1;         

        protected double currentEMA46_5m = -1;
        protected double currentEMA50_5m = -1;        
        protected override void AddCustomDataSeries()
        {
            // Add data series
            AddDataSeries(BarsPeriodType.Minute, 15);
            AddDataSeries(BarsPeriodType.Minute, 5);
            AddDataSeries(BarsPeriodType.Minute, 1);
        }
        protected override void AddCustomIndicators()
        {
            
        }

        protected override void OnStateChange_DataLoaded()
        {
            EMA46Indicator_5m = EMA(BarsArray[2], 46);
            EMA46Indicator_5m.Plots[0].Brush = Brushes.Black;

            EMA50Indicator_5m = EMA(BarsArray[2], 50);
            EMA50Indicator_5m.Plots[0].Brush = Brushes.Red;
            EMA50Indicator_5m.Plots[0].DashStyleHelper = DashStyleHelper.Dash;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "FishTrend";
            Description = @"Use EMA46/50 khung 5 phút và các cây nến khung 5 phút";

            StartDayTradeTime = new TimeSpan(6, 59, 0); // 6:59:00 am 
            EndDayTradeTime = new TimeSpan(16, 0, 0); // 2:00:00 pm

            AddPlot(Brushes.Black, "EMA46_5m");
            AddPlot(Brushes.Red, "EMA50_5m");

            Position_5m = GeneralEMAsPosition.Unknown;

            FullSizeATMName = "FishTrend_Reverse";
            HalfSizefATMName = "FishTrend_Follow";
        }

        private void DrawKey(int barIndex, double high, double low)
        {
            Draw.Line(this, $"5m_HIGH_{CurrentBar}", false, 1, high, -1, high, Brushes.Green, DashStyleHelper.Solid, 2);
            Draw.Line(this, $"5m_LOW_{CurrentBar}", false, 1, low, -1, low, Brushes.Green, DashStyleHelper.Solid, 2);
            Draw.Line(this, $"5m_VERTICAL_{CurrentBar}", false, 0, low, 0, high, Brushes.Green, DashStyleHelper.Solid, 2);            

            Draw.Line(this, $"1m_POINT_LOW_{CurrentBars[3]}", false,1, Lows[3][0], -1, Lows[3][0], Brushes.Gray, DashStyleHelper.Solid, 2);
            Draw.Line(this, $"1m_POINT_High_{CurrentBars[3]}", false, 1, Highs[3][0], -1, Highs[3][0], Brushes.Gray, DashStyleHelper.Solid, 2);

            Draw.Text(this, $"5m_TEXT_HIGH_{CurrentBar}", true, $"{high:N2}", 0, high + 0.5, 5, Brushes.Green,
                new SimpleFont("Arial", 9),
                TextAlignment.Left,
                Brushes.Transparent,
                Brushes.Transparent, 0);

            Draw.Text(this, $"5m_TEXT_LOW_{CurrentBar}", true, $"{low:N2}", 0, low - 1, 5, Brushes.Green,
                new SimpleFont("Arial", 9),
                TextAlignment.Left,
                Brushes.Transparent,
                Brushes.Transparent, 0);

            // Draw current line 
            Draw.HorizontalLine(this, $"5m_HIGH_Current", high, Brushes.Orange, DashStyleHelper.Dot, 2);
            Draw.HorizontalLine(this, $"5m_LOW_Current", low, Brushes.Orange, DashStyleHelper.Dot, 2);
            
        }

        protected override void OnBarUpdate()
		{
            #region Hiển thị indicators (Plot)
            try
            {
                Values[0][0] = EMA46Indicator_5m.Value[0];
                Values[1][0] = EMA50Indicator_5m.Value[0];
            }
            catch (Exception ex)
            {
                LocalPrint("[OnBarUpdate]: ERROR:" + ex.Message);
            }            

            if (BarsInProgress == 0 || CurrentBar < 60)                    
            {
                // Current View --> return
                return;
            }
            #endregion

            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1)
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                LocalPrint($"LOW KEY: {KeyLevel_5m_LOW:N2}, HIGH: {KeyLevel_5m_HIGH:N2}");

                BasicActionForTrading(TimeFrameToTrade.OneMinute);
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                // Detect new keys
                double highPrice_5m = High[0];
                double lowPrice_5m = Low[0];
                double openPrice_5m = Open[0];
                double closePrice_5m = Close[0];

                var maxEma_Current = StrategiesUtilities.MaxOfArray(EMA46Indicator_5m.Value[0], EMA50Indicator_5m.Value[0]);
                var minEma_Current = StrategiesUtilities.MinOfArray(EMA46Indicator_5m.Value[0], EMA50Indicator_5m.Value[0]);

                if (highPrice_5m > maxEma_Current && lowPrice_5m < minEma_Current)
                {
                    if (Last5mBarTouchEMA50 != CurrentBar - 1)
                    {
                        LocalPrint($"[CONFIRM] Found new range to trade. Low: {lowPrice_5m:N2}, High: {highPrice_5m:N2}");

                        KeyLevel_5m_HIGH = highPrice_5m;
                        KeyLevel_5m_LOW = lowPrice_5m;

                        DrawKey(CurrentBar, highPrice_5m, lowPrice_5m);

                        Last5mBarTouchEMA50 = CurrentBar;
                    }
                }
                else if (lowPrice_5m > maxEma_Current)
                {
                    Position_5m = GeneralEMAsPosition.Above;
                }
                else if (highPrice_5m < minEma_Current)
                {
                    Position_5m = GeneralEMAsPosition.Below;
                }
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 15) //15 minute
            {
                // Do nothing for now
            }
        }

        protected override void EnterOrder_Historial(FishTrendTradeDetail tradeAction)
        {
            base.EnterOrder_Historial(tradeAction);
        }

        protected override void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string atmStragtegyName, int quantity, bool isBuying, bool isSelling, OrderType orderType = OrderType.Limit)
        {
            // Inherit base class with Order Type is StopLimit
            base.EnterOrderPure(priceToSet, targetInTicks, stoplossInTicks, atmStragtegyName, quantity, isBuying, isSelling, OrderType.StopLimit);
        }

        protected override void BasicActionForTrading(TimeFrameToTrade timeFrameToTrade)
        {
            // Make sure each stratergy have each own time frame to trade
            if (timeFrameToTrade != Configured_TimeFrameToTrade)
            {
                return;
            }

            if (TradingStatus == TradingStatus.Idle)
            {
                var passTradeCondition = CheckingTradeCondition();
                if (!passTradeCondition)
                {
                    LocalPrint($"[BasicActionForTrading] Not Pass Condition to trade");
                    return;
                }

                var shouldTrade = ShouldTrade();                

                if (shouldTrade.Action != GeneralTradeAction.NoTrade) // Nếu chưa enter order thì mới enter order
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

            }   
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
                CancelAllPendingOrder();

                return;
            }

            var checkShouldTradeAgain = ShouldTrade();

            if (checkShouldTradeAgain.Action == GeneralTradeAction.NoTrade)
            {
                LocalPrint($"Check lại các điều kiện với [ShouldTrade], new answer: [{checkShouldTradeAgain.Action}] --> Cancel lệnh do không thỏa mãn các điều kiện trade");

                CancelAllPendingOrder();

                return;
            }
            else if (CurrentTradeAction.Action != checkShouldTradeAgain.Action)
            {   
                CancelAllPendingOrder();

                EnterOrder(checkShouldTradeAgain);             
            }
        }

        protected override void EnterOrderPureUsingTicks(double priceToSet, double targetInTicks, double stoplossInTicks, string signal, int quantity, bool isBuying, bool isSelling)
        {
            var text = isBuying ? "LONG" : "SHORT";

            if (isBuying)
            {
                var stopPrice = priceToSet - stoplossInTicks * TickSize; 
                EnterLongStopLimit(0, true, quantity, priceToSet, stopPrice, signal);
            }
            else
            {
                var stopPrice = priceToSet + stoplossInTicks * TickSize;
                EnterShortStopLimit(0, true, quantity, priceToSet, stopPrice, signal);
            }

            SetStopLoss(signal, CalculationMode.Ticks, stoplossInTicks, false);

            SetProfitTarget(signal, CalculationMode.Ticks, targetInTicks);

            LocalPrint($"Enter {text} for {quantity} contracts with signal [{signal}] at {priceToSet:N2}, stop loss ticks: {stoplossInTicks:N2}, target ticks: {targetInTicks:N2}");
        }

        protected override double GetSetPrice(FishTrendTradeDetail tradeAction, AtmStrategy additionalInfo)
        {
            if (tradeAction.Action == GeneralTradeAction.Sell)
            {
                return tradeAction.Direction == TradeDirection.Reverse ? KeyLevel_5m_LOW : KeyLevel_5m_LOW - AdjustmentPoint;
            }
            else if (tradeAction.Action == GeneralTradeAction.Buy)
            {
                return tradeAction.Direction == TradeDirection.Reverse ? KeyLevel_5m_HIGH : KeyLevel_5m_HIGH + AdjustmentPoint;
            }
            return -1; 
        }

        protected override FishTrendTradeDetail ShouldTrade()
        {
            var close_1m = Close[0];
            var open_1m = Open[0];

            if (close_1m <= KeyLevel_5m_HIGH && close_1m >= KeyLevel_5m_LOW && CandleUtilities.IsRedCandle(close_1m, open_1m))
            {
                return new FishTrendTradeDetail
                {
                    Action = GeneralTradeAction.Sell,
                    Sizing = TradeSizingEnum.Big,

                    // Các cây nến từ dưới đi lên, chạm vào EMA50 và bật xuống lại. 
                    // Như vậy trend đang là trend đi lên
                    Direction = Position_5m == GeneralEMAsPosition.Below ? TradeDirection.Reverse : TradeDirection.Trending
                };
            }
            else if (close_1m <= KeyLevel_5m_HIGH && close_1m >= KeyLevel_5m_LOW && CandleUtilities.IsGreenCandle(close_1m, open_1m))
            {
                return new FishTrendTradeDetail
                {
                    Action = GeneralTradeAction.Buy,
                    Sizing = TradeSizingEnum.Big,

                    // Các cây nến từ trên đi xuống, chạm vào EMA50 và bật lên lại. 
                    // Như vậy trend đang là trend xuống
                    Direction = Position_5m == GeneralEMAsPosition.Above ? TradeDirection.Reverse : TradeDirection.Trending
                };
            }
            else
            {
                return new FishTrendTradeDetail
                {
                    Action = GeneralTradeAction.NoTrade,
                    Sizing = TradeSizingEnum.Big
                };
            }
        }
    }
}
