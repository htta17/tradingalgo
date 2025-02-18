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
using System.Security.Cryptography;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Vikki : BarClosedBaseClass<TradeAction, TradeAction>
	{
        protected override bool IsBuying => throw new NotImplementedException();

        protected override bool IsSelling => throw new NotImplementedException();

        private EMA ema20_5m;
        private EMA ema50_5m;
        private EMA ema100_5m;
        private RSI rsi_5m;

        protected override void OnStateChange()
		{
            if (State == State.SetDefaults)
            {
                Description = @"EMA 20/50/100 + RSI 40/60s.";
                Name = "Vikki";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 100;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                ema20_5m = EMA(20);
                ema20_5m.Plots[0].Brush = Brushes.DarkCyan;

                ema50_5m = EMA(50);
                ema50_5m.Plots[0].Brush = Brushes.DeepPink;

                ema100_5m = EMA(100);

                rsi_5m = RSI(14, 2);

                AddChartIndicator(ema20_5m);
                AddChartIndicator(ema50_5m);
                AddChartIndicator(ema100_5m);
                AddChartIndicator(rsi_5m);
            }
		}

		protected override void OnBarUpdate()
		{
            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                if (TradingStatus == TradingStatus.Idle)
                {
                    if (rsi_5m[0] > 40 && (CrossAbove(ema20_5m, ema50_5m, 1) || CrossAbove(ema20_5m, ema50_5m, 2)) && Close[0] > ema100_5m[0])
                    {
                        EnterLong(StrategiesUtilities.SignalEntry_VikkiFull);
                        SetStopLoss(StrategiesUtilities.SignalEntry_VikkiFull, CalculationMode.Ticks, StopLossInTicks, false);
                        SetProfitTarget(StrategiesUtilities.SignalEntry_VikkiFull, CalculationMode.Ticks, Target2InTicks, false);
                    }
                    else if (rsi_5m[0] < 60 && (CrossBelow(ema20_5m, ema50_5m, 1) || CrossBelow(ema20_5m, ema50_5m, 2)) && Close[0] < ema100_5m[0])
                    {
                        EnterShort(StrategiesUtilities.SignalEntry_VikkiFull);
                        SetStopLoss(StrategiesUtilities.SignalEntry_VikkiFull, CalculationMode.Ticks, StopLossInTicks, false);
                        SetProfitTarget(StrategiesUtilities.SignalEntry_VikkiFull, CalculationMode.Ticks, Target2InTicks, false);
                    }
                }
            }
        }

        protected override double GetStopLossPrice(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetSetPrice(TradeAction tradeAction)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Half(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Full(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override TradeAction ShouldTrade()
        {
            throw new NotImplementedException();
        }

        protected override bool IsHalfPriceOrder(Order order)
        {
            throw new NotImplementedException();
        }

        protected override bool IsFullPriceOrder(Order order)
        {
            throw new NotImplementedException();
        }
    }
}
