#region Using declarations
using System;
using System.Windows.Media;
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

        private Series<double> macdDiff;
        private Series<double> bbUpper;
        private Series<double> bbLower;
        private Series<double> trendUp;
        private Series<double> trendDown;
        private Series<double> explosionLine;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Waddah Attar Explosion Indicator (Converted from Pine Script)";
                Name = "WaddahAttarExplosion";
                IsOverlay = false;

                AddPlot(Brushes.Lime, "UpTrend");
                AddPlot(Brushes.Red, "DownTrend");
                AddPlot(Brushes.SaddleBrown, "ExplosionLine");
                AddPlot(Brushes.Blue, "DeadZoneLine");

                // Set the number of bars required for calculation
                BarsRequiredToPlot = Math.Max(fastLength, Math.Max(slowLength, channelLength)) + 1;
            }
            else if (State == State.DataLoaded)
            {
                macdDiff = new Series<double>(this);
                bbUpper = new Series<double>(this);
                bbLower = new Series<double>(this);
                trendUp = new Series<double>(this);
                trendDown = new Series<double>(this);
                explosionLine = new Series<double>(this);
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

                Print("typicalPrice");

                // Dead Zone Calculation
                double deadZone = EMA(TypicalPriceSeries(), 100)[0] * 3.7;

                Print("deadZone");

                // MACD Difference Calculation
                double fastEMA = EMA(Close, fastLength)[0];
                Print("fastEMA");

                double slowEMA = EMA(Close, slowLength)[0];
                Print("slowEMA");
                double prevFastEMA = EMA(Close, fastLength)[1];
                Print("prevFastEMA");
                double prevSlowEMA = EMA(Close, slowLength)[1];
                Print("prevSlowEMA");

                double macd = fastEMA - slowEMA;
                Print("macd");
                double prevMacd = prevFastEMA - prevSlowEMA;
                Print("prevMacd");
                double t1 = (macd - prevMacd) * sensitivity;
                Print("t1");

                // Bollinger Bands Calculation
                double bbBasis = SMA(Close, channelLength)[0];
                Print("bbBasis");
                double bbDev = mult * StdDev(Close, channelLength)[0];
                Print("bbDev");
                double bbUpperVal = bbBasis + bbDev;
                double bbLowerVal = bbBasis - bbDev;

                // Explosion Line
                double e1 = bbUpperVal - bbLowerVal;

                // Trend Calculations
                trendUp[0] = t1 >= 0 ? t1 : 0;
                trendDown[0] = t1 < 0 ? -t1 : 0;
                Print("before Plot");

                // Set plot values
                Values[0][0] = trendUp[0]; // UpTrend
                Values[1][0] = trendDown[0]; // DownTrend
                Values[2][0] = e1; // ExplosionLine
                Values[3][0] = deadZone; // DeadZoneLine

                Print($"{Time[0]} UpTrend: {trendUp[0]}, DownTrend: {trendDown[0]}, ExplosionLine: {e1}, DeadZoneLine: {deadZone}");
            }
            catch (Exception e)
            {
                Print(e.Message);
            }
        }

        // Helper function for Typical Price
        private Series<double> TypicalPriceSeries()
        {
            var typicalPriceSeries = new Series<double>(this);
            for (int i = 0; i < CurrentBar; i++)
                typicalPriceSeries[i] = (High[i] + Low[i] + Close[i]) / 3.0;
            return typicalPriceSeries;
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
