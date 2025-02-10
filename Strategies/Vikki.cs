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
                Description = @"Enter the description for your new custom Strategy here.";
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
                AddPlot(Brushes.Blue, "EMA20");
                AddPlot(Brushes.Red, "EMA50");
                AddPlot(Brushes.Green, "EMA100");
            }
            else if (State == State.DataLoaded)
            {
                ema20_5m = EMA(20);
                ema50_5m = EMA(50);
                ema100_5m = EMA(100);
                rsi_5m = RSI(14, 2);
            }
		}

		protected override void OnBarUpdate()
		{
			//Add your custom strategy logic here.
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
