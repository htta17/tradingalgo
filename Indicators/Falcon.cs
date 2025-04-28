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
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class Falcon : Indicator
	{
		private EMA ema21;
        private double angle;

        [Range(1, int.MaxValue), NinjaScriptProperty]
        public int LookbackBars { get; set; } = 20;

		[Range(1, int.MaxValue), NinjaScriptProperty]
		public int MinimumRangeToTrade { get; set; } = 45;

        public readonly int RANGE_20_NO_TRD = 20;
        public readonly int RANGE_45_NO_TRD = 45;
        public readonly int RANGE_55_YES_TRD = 55;
        public readonly int RANGE_70_YES_TRD = 70;

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "Falcon";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				
				AddPlot(new Stroke(Brushes.DarkOrange, 2), PlotStyle.Bar, "EMA Angle (deg)");
			}
			else if (State == State.Configure)
            {
                ema21 = EMA(21);
                //AddDataSeries(Data.BarsPeriodType.Minute, 1); // Optional, keep it if working with multiple timeframes
            }
		}

        protected override void OnBarUpdate()
		{
			//Add your custom indicator logic here.

			if (CurrentBar < LookbackBars)
				return;

			double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

			for (int i = 0; i < LookbackBars; i++)
			{
				double x = i;
				double y = ema21[i];

				sumX += x;
				sumY += y;
				sumXY += x * y;
				sumX2 += x * x;
			}

			// Linear regression slope (m)
			double numerator = LookbackBars * sumXY - sumX * sumY;
			double denominator = LookbackBars * sumX2 - sumX * sumX;

			if (denominator == 0)
			{
				Values[0][0] = 0;
				return;
			}

			double slope = numerator / denominator;

			// Convert slope to angle in degrees
			double angle = Math.Atan(slope) * (180.0 / Math.PI);

			var absolutedAngle = Math.Abs(angle);

			Values[0][0] = -angle;

			/*
			if (absolutedAngle < RANGE_20_NO_TRD)
			{
				PlotBrushes[0][0] = Brushes.Black;
			}
			else if (absolutedAngle >= RANGE_20_NO_TRD && absolutedAngle < RANGE_45_NO_TRD)
			{
				PlotBrushes[0][0] = Brushes.Green;
			}
			*/
			if (absolutedAngle < MinimumRangeToTrade)
			{
                PlotBrushes[0][0] = Brushes.DarkGray;
            }
			else if (absolutedAngle >= MinimumRangeToTrade && absolutedAngle < RANGE_55_YES_TRD)
			{
				PlotBrushes[0][0] = Brushes.Orange;
			}
			else if (absolutedAngle >= RANGE_55_YES_TRD && absolutedAngle < RANGE_70_YES_TRD)
			{
				PlotBrushes[0][0] = Brushes.Blue;
			}
			else
			{
				PlotBrushes[0][0] = Brushes.DeepPink;
			}
		}
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Falcon[] cacheFalcon;
		public Falcon Falcon(int lookbackBars, int minimumRangeToTrade)
		{
			return Falcon(Input, lookbackBars, minimumRangeToTrade);
		}

		public Falcon Falcon(ISeries<double> input, int lookbackBars, int minimumRangeToTrade)
		{
			if (cacheFalcon != null)
				for (int idx = 0; idx < cacheFalcon.Length; idx++)
					if (cacheFalcon[idx] != null && cacheFalcon[idx].LookbackBars == lookbackBars && cacheFalcon[idx].MinimumRangeToTrade == minimumRangeToTrade && cacheFalcon[idx].EqualsInput(input))
						return cacheFalcon[idx];
			return CacheIndicator<Falcon>(new Falcon(){ LookbackBars = lookbackBars, MinimumRangeToTrade = minimumRangeToTrade }, input, ref cacheFalcon);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Falcon Falcon(int lookbackBars, int minimumRangeToTrade)
		{
			return indicator.Falcon(Input, lookbackBars, minimumRangeToTrade);
		}

		public Indicators.Falcon Falcon(ISeries<double> input , int lookbackBars, int minimumRangeToTrade)
		{
			return indicator.Falcon(input, lookbackBars, minimumRangeToTrade);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Falcon Falcon(int lookbackBars, int minimumRangeToTrade)
		{
			return indicator.Falcon(Input, lookbackBars, minimumRangeToTrade);
		}

		public Indicators.Falcon Falcon(ISeries<double> input , int lookbackBars, int minimumRangeToTrade)
		{
			return indicator.Falcon(input, lookbackBars, minimumRangeToTrade);
		}
	}
}

#endregion
