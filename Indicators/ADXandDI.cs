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
	public class ADXandDI : Indicator
	{
        private Series<double> trueRange;
        private Series<double> directionalMovementPlus;
        private Series<double> directionalMovementMinus;
        private Series<double> smoothedTrueRange;
        private Series<double> smoothedDirectionalMovementPlus;
        private Series<double> smoothedDirectionalMovementMinus;
        private Series<double> dx;
        private SMA adx;

        [Range(1, int.MaxValue), NinjaScriptProperty]
        public int Length { get; set; } = 14;

        [Range(1, int.MaxValue), NinjaScriptProperty]
        public double ThresholdUpper { get; set; } = 22;

        [Range(1, int.MaxValue), NinjaScriptProperty]
        public double ThresholdLower { get; set; } = 18.5;

        protected override void OnStateChange()
		{
            if (State == State.SetDefaults)
            {
                AddPlot(new Stroke(Brushes.Goldenrod, DashStyleHelper.Dash, 2), PlotStyle.Line, "ADX");

                AddPlot(Brushes.Green, "DI+");
                AddPlot(Brushes.Red, "DI-");
                
                AddLine(new Stroke(Brushes.Green, DashStyleHelper.Dash, 1.5f), ThresholdLower, "Enter");
                AddLine(new Stroke(Brushes.Red, DashStyleHelper.Dash, 1.5f), ThresholdUpper, "Cancel");
            }
            else if (State == State.DataLoaded)
            {
                trueRange = new Series<double>(this, MaximumBarsLookBack.Infinite);
                directionalMovementPlus = new Series<double>(this, MaximumBarsLookBack.Infinite);
                directionalMovementMinus = new Series<double>(this, MaximumBarsLookBack.Infinite);
                smoothedTrueRange = new Series<double>(this, MaximumBarsLookBack.Infinite);
                smoothedDirectionalMovementPlus = new Series<double>(this, MaximumBarsLookBack.Infinite);
                smoothedDirectionalMovementMinus = new Series<double>(this, MaximumBarsLookBack.Infinite);
                dx = new Series<double>(this, MaximumBarsLookBack.Infinite);
                adx = SMA(dx, Length);
            }
        }

		protected override void OnBarUpdate()
		{
            if (CurrentBar == 0)
                return;

            trueRange[0] = Math.Max(Math.Max(High[0] - Low[0], Math.Abs(High[0] - Close[1])), Math.Abs(Low[0] - Close[1]));
            directionalMovementPlus[0] = (High[0] - High[1] > Low[1] - Low[0]) ? Math.Max(High[0] - High[1], 0) : 0;
            directionalMovementMinus[0] = (Low[1] - Low[0] > High[0] - High[1]) ? Math.Max(Low[1] - Low[0], 0) : 0;

            if (CurrentBar < Length)
                return;

            smoothedTrueRange[0] = smoothedTrueRange[1] - (smoothedTrueRange[1] / Length) + trueRange[0];
            smoothedDirectionalMovementPlus[0] = smoothedDirectionalMovementPlus[1] - (smoothedDirectionalMovementPlus[1] / Length) + directionalMovementPlus[0];
            smoothedDirectionalMovementMinus[0] = smoothedDirectionalMovementMinus[1] - (smoothedDirectionalMovementMinus[1] / Length) + directionalMovementMinus[0];

            double diPlus = (smoothedDirectionalMovementPlus[0] / smoothedTrueRange[0]) * 100;
            double diMinus = (smoothedDirectionalMovementMinus[0] / smoothedTrueRange[0]) * 100;
            dx[0] = (Math.Abs(diPlus - diMinus) / (diPlus + diMinus)) * 100;

            Values[0][0] = adx[0];
            Values[1][0] = diPlus;
            Values[2][0] = diMinus;
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ADXandDI[] cacheADXandDI;
		public ADXandDI ADXandDI(int length, double thresholdUpper, double thresholdLower)
		{
			return ADXandDI(Input, length, thresholdUpper, thresholdLower);
		}

		public ADXandDI ADXandDI(ISeries<double> input, int length, double thresholdUpper, double thresholdLower)
		{
			if (cacheADXandDI != null)
				for (int idx = 0; idx < cacheADXandDI.Length; idx++)
					if (cacheADXandDI[idx] != null && cacheADXandDI[idx].Length == length && cacheADXandDI[idx].ThresholdUpper == thresholdUpper && cacheADXandDI[idx].ThresholdLower == thresholdLower && cacheADXandDI[idx].EqualsInput(input))
						return cacheADXandDI[idx];
			return CacheIndicator<ADXandDI>(new ADXandDI(){ Length = length, ThresholdUpper = thresholdUpper, ThresholdLower = thresholdLower }, input, ref cacheADXandDI);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ADXandDI ADXandDI(int length, double thresholdUpper, double thresholdLower)
		{
			return indicator.ADXandDI(Input, length, thresholdUpper, thresholdLower);
		}

		public Indicators.ADXandDI ADXandDI(ISeries<double> input , int length, double thresholdUpper, double thresholdLower)
		{
			return indicator.ADXandDI(input, length, thresholdUpper, thresholdLower);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ADXandDI ADXandDI(int length, double thresholdUpper, double thresholdLower)
		{
			return indicator.ADXandDI(Input, length, thresholdUpper, thresholdLower);
		}

		public Indicators.ADXandDI ADXandDI(ISeries<double> input , int length, double thresholdUpper, double thresholdLower)
		{
			return indicator.ADXandDI(input, length, thresholdUpper, thresholdLower);
		}
	}
}

#endregion
