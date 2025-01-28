#region Using declarations
using System;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class WaddahAttarExplosion : Indicator
    {
        // Inputs
        private int sensitivity = 150;
        private int fastLength = 20;
        private int slowLength = 40;
        private int channelLength = 20;
        private double mult = 2.0;
        
        private Series<double> trendUp;
        private Series<double> trendDown;        
        private Series<double> deadZoneSeries;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Waddah Attar Explosion Indicator (Converted from Pine Script)";
                Name = "WaddahAttarExplosion";
                IsOverlay = false;

                AddPlot(new Stroke(Brushes.Green, 2), PlotStyle.Bar, "UpTrend");
                AddPlot(new Stroke(Brushes.Red, 2), PlotStyle.Bar, "DownTrend");
                AddPlot(new Stroke(Brushes.LimeGreen, 2), PlotStyle.Line, "ExplosionLine");
                AddPlot(new Stroke(Brushes.DarkRed, 2), PlotStyle.Line, "DeadZoneLine");

                // Set the number of bars required for calculation
                BarsRequiredToPlot = Math.Max(fastLength, Math.Max(slowLength, channelLength)) + 1;
            }
            else if (State == State.DataLoaded)
            {
                trendUp = new Series<double>(this);
                trendDown = new Series<double>(this);
                deadZoneSeries = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            // Calculate the maximum required lookback period
            int lookbackPeriod = Math.Max(fastLength, Math.Max(slowLength, channelLength));

            // Ensure there are enough bars before calculating
            if (CurrentBar < lookbackPeriod)
                return;

            try
            {
                // Calculate Typical Price
                double typicalPrice = (High[0] + Low[0] + Close[0]) / 3.0;

                // Calculate True Range and store it in a Series
                double trueRange = Math.Max(High[0] - Low[0], Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
                if (CurrentBar == 0)
                    deadZoneSeries[0] = trueRange; // Initialize the first value
                else
                    deadZoneSeries[0] = trueRange; // Store True Range for EMA calculation

                // Calculate smoothed ATR using EMA of the True Range Series
                double smoothedATR = EMA(deadZoneSeries, 100)[0];

                // Dead Zone
                double deadZone = smoothedATR * 3.7;


                //Print("deadZone");

                // MACD Difference Calculation
                double fastEMA = EMA(Close, fastLength)[0];

                double slowEMA = EMA(Close, slowLength)[0];
                double prevFastEMA = EMA(Close, fastLength)[1];
                double prevSlowEMA = EMA(Close, slowLength)[1];

                double macd = fastEMA - slowEMA;
                double prevMacd = prevFastEMA - prevSlowEMA;
                double t1 = (macd - prevMacd) * sensitivity;

                // Bollinger Bands Calculation
                double bbBasis = SMA(Close, channelLength)[0];
                double bbDev = mult * StdDev(Close, channelLength)[0];
                double bbUpperVal = bbBasis + bbDev;
                double bbLowerVal = bbBasis - bbDev;

                // Explosion Line
                double e1 = bbUpperVal - bbLowerVal;

                // Trend Calculations
                trendUp[0] = t1 >= 0 ? t1 : 0;
                trendDown[0] = t1 < 0 ? -t1 : 0;

                // Set plot values
                Values[0][0] = trendUp[0]; // UpTrend
                Values[1][0] = trendDown[0]; // DownTrend
                Values[2][0] = e1; // ExplosionLine
                Values[3][0] = deadZone; // DeadZoneLine
            }
            catch (Exception e)
            {
                Print(e.Message);
            }
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private WaddahAttarExplosion[] cacheWaddahAttarExplosion;
		public WaddahAttarExplosion WaddahAttarExplosion()
		{
			return WaddahAttarExplosion(Input);
		}

		public WaddahAttarExplosion WaddahAttarExplosion(ISeries<double> input)
		{
			if (cacheWaddahAttarExplosion != null)
				for (int idx = 0; idx < cacheWaddahAttarExplosion.Length; idx++)
					if (cacheWaddahAttarExplosion[idx] != null &&  cacheWaddahAttarExplosion[idx].EqualsInput(input))
						return cacheWaddahAttarExplosion[idx];
			return CacheIndicator<WaddahAttarExplosion>(new WaddahAttarExplosion(), input, ref cacheWaddahAttarExplosion);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.WaddahAttarExplosion WaddahAttarExplosion()
		{
			return indicator.WaddahAttarExplosion(Input);
		}

		public Indicators.WaddahAttarExplosion WaddahAttarExplosion(ISeries<double> input )
		{
			return indicator.WaddahAttarExplosion(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.WaddahAttarExplosion WaddahAttarExplosion()
		{
			return indicator.WaddahAttarExplosion(Input);
		}

		public Indicators.WaddahAttarExplosion WaddahAttarExplosion(ISeries<double> input )
		{
			return indicator.WaddahAttarExplosion(input);
		}
	}
}

#endregion
