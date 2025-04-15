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
using System.Reflection;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Roses : PriceChangedATMBasedClass<EMA2129OrderDetail, AtmStrategy>
	{
		public Roses() : base()
		{ 
		}

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Description = "Roses (ATM realtime)";
            Name = "EMA 21/29 1-min frame, trending.";

            FullSizeATMName = "Roses_Default_4cts";
            HalfSizefATMName = "Roses_Default_4cts";
            RiskyATMName = "Roses_Default_4cts";

            AddPlot(Brushes.Green, "EMA9_5m");
            AddPlot(Brushes.Red, "EMA46_5m");
            AddPlot(Brushes.Black, "EMA51_5m");

            EMA2129Status = new EMA2129Status();
            MaxiumOrderBeforeReset = 3; 
        }

        private EMA EMA29Indicator_1m { get; set; }
        private EMA EMA21Indicator_1m { get; set; }
        private EMA EMA46Indicator_5m { get; set; }
        private EMA EMA51Indicator_5m { get; set; }
        private EMA EMA9Indicator_5m { get; set; }

        private ADXandDI ADXandDI { get; set; }

        private EMA2129Status EMA2129Status { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Risky Strategy",
            Description = "Strategy sử dụng khi lệnh là risky",
            Order = 1, GroupName = StrategiesUtilities.Configuration_General_Name)]        
        public int MaxiumOrderBeforeReset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Value to Enter Order:",
            Description = "Nếu ADX value > [giá trị]: Enter order",
            Order = 2, GroupName = StrategiesUtilities.Configuration_General_Name)]
        public int ADXValueToEnterOrder { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Value to cancel Order:",
            Description = "Nếu ADX value < [giá trị]: Cancel order",
            Order = 3, GroupName = StrategiesUtilities.Configuration_General_Name)]
        public int ADXValueToCancelOrder { get; set; }

        protected override void AddIndicators()
        {
            EMA29Indicator_1m = EMA(BarsArray[2], 29);
            EMA29Indicator_1m.Plots[0].Brush = Brushes.Red;

            EMA21Indicator_1m = EMA(BarsArray[2], 21);
            EMA21Indicator_1m.Plots[0].Brush = Brushes.Blue;

            EMA46Indicator_5m = EMA(BarsArray[1], 46);
            EMA51Indicator_5m = EMA(BarsArray[1], 51);
            EMA9Indicator_5m = EMA(BarsArray[1], 10);

            ADXandDI = ADXandDI(14, 25, 20);            

            AddChartIndicator(EMA29Indicator_1m);
            AddChartIndicator(EMA21Indicator_1m);

            AddChartIndicator(ADXandDI);
        }

        protected override void OnBarUpdate_StateHistorical(int barsPeriod)
        {            
            if (barsPeriod == 1)
            {
                try
                {
                    // Print(EMA9Indicator_5m.Value[0]);
                    Values[0][0] = EMA9Indicator_5m.Value[0];
                    Values[1][0] = EMA46Indicator_5m.Value[0];
                    Values[2][0] = EMA51Indicator_5m.Value[0];
                }
                catch (Exception ex) 
                {
                    LocalPrint($"[OnBarUpdate_StateHistorical] - ERROR: {ex.Message}");
                }               
            }            
        }

        protected override void OnNewBarCreated(int barsPeriod)
        {
            var index = GetBarIndex(barsPeriod);

            LocalPrint($"1st tick of the bar {barsPeriod}-mins {DateTime.Now} - Hi: {Highs[index][0]:N2}, Lo: {Lows[index][0]:N2}, Open: {Opens[index][0]:N2}, Close: {Closes[index][0]:N2}");
        }

        protected override void OnCurrentBarClosed(int barsPeriod)
        {
            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();

            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            if (barsPeriod == 1)
            {
                var index = GetBarIndex(barsPeriod);

                var high = Highs[index][0];
                var low = Lows[index][0];
                var open = Opens[index][0];
                var close = Closes[index][0];

                var ema21Val = EMA21Indicator_1m.Value[0];
                var ema29Val = EMA29Indicator_1m.Value[0];
                
                if (high < Math.Min(ema21Val, ema29Val))
                {
                    if (EMA2129Status.Position == EMA2129Position.Unknown || EMA2129Status.Position == EMA2129Position.Above)
                    {
                        EMA2129Status.SetPosition(EMA2129Position.Below);

                        EMA2129Status.ResetCount();
                    }
                }
                else if (low >  Math.Max(ema21Val,ema29Val))
                {
                    if (EMA2129Status.Position == EMA2129Position.Unknown || EMA2129Status.Position == EMA2129Position.Below)
                    {
                        EMA2129Status.SetPosition(EMA2129Position.Above);

                        EMA2129Status.ResetCount();
                    }
                }

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    if (shouldTrade.Action != GeneralTradeAction.NoTrade)
                    {
                        EnterOrder(shouldTrade);
                    }
                }

                
            }            
        }
        protected override void OnRegularTick(int barsPeriod)
        {
            var index = GetBarIndex(barsPeriod);
            if (barsPeriod == 5)
            {
                /*
                var ema46 = EMA46Indicator_5m.Value[0];
                var ema51 = EMA51Indicator_5m.Value[0];
                var ema9 = EMA9Indicator_5m.Value[0];

                DrawLine("ema46", ema46, Brushes.Black, Brushes.Black, labelText: $"EMA46 (5): {ema46:N2}");
                DrawLine("ema51", ema51, Brushes.Red, Brushes.Red, textPosition: 3, labelText: $"EMA51 (5): {ema51:N2}");
                DrawLine("ema9", ema9, Brushes.Green, Brushes.Green, textPosition: -6, labelText: $"EMA9 (5): {ema9:N2}");                
                */
            }
            else if (barsPeriod == 1)
            {
                var ema9 = EMA9Indicator_5m.Value[0];
                var ema46 = EMA46Indicator_5m.Value[0];
                var ema51 = EMA51Indicator_5m.Value[0];

                
                Values[0][0] = ema9;
                Values[1][0] = ema46;
                Values[2][0] = ema51;                   
            }
        }

        protected override EMA2129OrderDetail ShouldTrade()
        {
            var notradeDetail = new EMA2129OrderDetail
            {
                Action = GeneralTradeAction.NoTrade,
                Postition = EMA2129OrderPostition.NoTrade,
                Sizing = EMA2129SizingEnum.Small
            };
            if (ADXandDI.Value[0] < ADXValueToCancelOrder)
            {
                return notradeDetail;
            }
            else // ADXandDI.Value[0] >= ADXValueToCancelOrder
            {
                if (EMA2129Status.Position == EMA2129Position.Below)
                {
                    var action = GeneralTradeAction.Sell;
                    var position = EMA2129OrderPostition.EMA21;
                    var sizing = EMA2129SizingEnum.Medium;

                    return new EMA2129OrderDetail
                    {
                        Action = action, 
                        Postition = position,
                        Sizing = sizing
                    };
                }
                else if (EMA2129Status.Position == EMA2129Position.Above)
                {
                    var action = GeneralTradeAction.Buy;
                    var position = EMA2129OrderPostition.EMA21;
                    var sizing = EMA2129SizingEnum.Medium;

                    return new EMA2129OrderDetail
                    {
                        Action = action,
                        Postition = position,
                        Sizing = sizing
                    };
                }
            }

            return notradeDetail;

        }

        protected override void EnterOrderHistorical(EMA2129OrderDetail detail)
        {
            
        }

        protected override void TransitionOrdersToLive()
        {
            throw new NotImplementedException();
        }

        protected double FilledPrice = -1;
        protected double StopLossPrice = -1;
        protected double TargetPrice_Full = -1;
        protected double TargetPrice_Half = -1;


        protected override void EnterOrderRealtime(EMA2129OrderDetail detail)
        {
            // Set global values
            CurrentTradeAction = detail;            

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var action = detail.Action == GeneralTradeAction.Buy ? OrderAction.Buy : OrderAction.Sell;

            // Get stop loss and target ID based on strategy 
            var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(detail);

            double priceToSet = GetSetPrice(detail, atmStrategy);

            double stopLoss = GetStopLossPrice(detail, priceToSet, atmStrategy);

            var targetHalf = GetTargetPrice_Half(detail, priceToSet, atmStrategy);

            var targetFull = GetTargetPrice_Full(detail, priceToSet, atmStrategy);

            FilledPrice = priceToSet;

            StopLossPrice = stopLoss;

            TargetPrice_Half = targetHalf;

            TargetPrice_Full = targetFull;

            //CurrentChosenStrategy = atmStrategyName;

            LocalPrint($@"Enter {action}. Price to set: {priceToSet:N2}, StopLossPrice: {StopLossPrice:N2}, Target 1: {TargetPrice_Half:N2}, Target Full: {TargetPrice_Full:N2}");

            try
            {
                //EnterOrderPure(priceToSet, 0, 0, atmStrategyName, 0, IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        protected override double GetSetPrice(EMA2129OrderDetail tradeAction, AtmStrategy additional)
        {
            double ans = -1; 

            if (tradeAction.Postition == EMA2129OrderPostition.EMA21)
            {
                ans = EMA21Indicator_1m.Value[0]; 
            }

            return ans;
        }

        protected override double GetTargetPrice_Half(EMA2129OrderDetail tradeAction, double setPrice, AtmStrategy additional)
        {
            var isBuying = tradeAction.Action == GeneralTradeAction.Buy;
            var targetTick_Half = isBuying ? additional.Brackets.Min(c => c.Target) : additional.Brackets.Max(c => c.Target);

            return isBuying ?
                setPrice + targetTick_Half * TickSize :
                setPrice - targetTick_Half * TickSize;
        }

        protected override double GetTargetPrice_Full(EMA2129OrderDetail tradeAction, double setPrice, AtmStrategy additional)
        {
            var isBuying = tradeAction.Action == GeneralTradeAction.Buy;
            var targetTick_Full = isBuying ? additional.Brackets.Max(c => c.Target) : additional.Brackets.Min(c => c.Target);

            return isBuying ?
                setPrice + targetTick_Full * TickSize :
                setPrice - targetTick_Full * TickSize;
        }

        protected override double GetStopLossPrice(EMA2129OrderDetail tradeAction, double setPrice, AtmStrategy additional)
        {
            var stopLossTick = additional.Brackets[0].StopLoss;
            var isBuying = tradeAction.Action == GeneralTradeAction.Buy;

            return isBuying ?
                setPrice - stopLossTick * TickSize :
                setPrice + stopLossTick * TickSize;
        }
    }
}
