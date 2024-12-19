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
	public class TrendingEMA2951 : Strategy
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"EMA200, EMA51, EMA29 khung 1 phút. Trending sử dụng ";
				Name = "TrendingEMA2951";
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
				BarsRequiredToTrade = 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration = true;
			}
			else if (State == State.Configure)
			{
                AddDataSeries(BarsPeriodType.Minute, 1);
                AddDataSeries(BarsPeriodType.Minute, 5);				
			}
			else if (State == State.DataLoaded)
			{
				var ema29 = EMA(29);
                ema29.Plots[0].Brush = Brushes.Goldenrod;
                AddChartIndicator(ema29);

                var ema51 = EMA(51);
                ema51.Plots[0].Brush = Brushes.Green;
                AddChartIndicator(ema51);

                var ema89 = EMA(89);
                ema89.Plots[0].Brush = Brushes.DarkCyan;				
                AddChartIndicator(ema89);

                var ema120 = EMA(120);
                ema120.Plots[0].Brush = Brushes.DarkCyan;                
                AddChartIndicator(ema120);
            }
        }

		protected override void OnBarUpdate()
		{
            if (CurrentBar < 120)
                return;

            Draw.Region(this, "Zone", CurrentBar - 50, CurrentBar, EMA(89), EMA(120), null, Brushes.LightBlue, 50);
        }
	}
}
