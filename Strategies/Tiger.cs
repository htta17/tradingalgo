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
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Tiger : Strategy
	{
        private DateTime LastExecutionTime = DateTime.MinValue;

        private int executionCount = 0;

        #region Một số key levels quan trọng
        private double YesterdayHigh = -1;
        private double YesterdayLow = -1;
        private double YesterdayMiddle = -1;

        private double PreMarketHigh = double.MinValue;
        private double PreMarketLow = double.MaxValue;
        private double PreMarketMiddle = 0;

        private double AsianHigh = double.MinValue;
        private double AsianLow = double.MaxValue;
        private double AsianMiddle = double.MaxValue;
        #endregion

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Trade theo live time, dựa theo WAE cho trending và Bollinger band + price action cho reverse.";
				Name										= Name = this.Name;
                Calculate									= Calculate.OnPriceChange;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
			}
			else if (State == State.Configure)
			{
			}
		}

        private void FindAndDrawKeyLevels()
        {
            if (Bars.IsFirstBarOfSession)
            {
                YesterdayHigh = PriorDayOHLC().PriorHigh[0];
                YesterdayLow = PriorDayOHLC().PriorLow[0];
                YesterdayMiddle = (YesterdayHigh + YesterdayLow) / 2;
                Draw.HorizontalLine(this, "YesterdayHigh", YesterdayHigh, Brushes.Blue, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "YesterdayLow", YesterdayLow, Brushes.Blue, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "YesterdayMiddle", YesterdayMiddle, Brushes.Blue, DashStyleHelper.Dash, 1);

                PreMarketHigh = double.MinValue;
                PreMarketLow = double.MaxValue;
                AsianHigh = double.MinValue;
                AsianLow = double.MaxValue;
            }

            // Define pre-market time range (12:00 AM to 8:30 AM CST)
            if (ToTime(Time[0]) >= 0 && ToTime(Time[0]) < 83000)
            {
                PreMarketHigh = Math.Max(PreMarketHigh, High[0]);
                PreMarketLow = Math.Min(PreMarketLow, Low[0]);
                PreMarketMiddle = (PreMarketLow + PreMarketHigh) / 2;
            }
            // Define Asian session time range (6:00 PM to 3:00 AM CST)
            if (ToTime(Time[0]) >= 180000 || ToTime(Time[0]) < 30000)
            {
                AsianHigh = Math.Max(AsianHigh, High[0]);
                AsianLow = Math.Min(AsianLow, Low[0]);
                AsianMiddle = (AsianHigh + AsianLow) / 2;
            }

            if (ToTime(Time[0]) == 83000)
            {
                Draw.HorizontalLine(this, "PreMarketHigh", PreMarketHigh, Brushes.Orange, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "PreMarketLow", PreMarketLow, Brushes.Orange, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "PreMarketMiddle", PreMarketMiddle, Brushes.Orange, DashStyleHelper.Dash, 1);
            }
            else if (ToTime(Time[0]) == 30000)
            {
                Draw.HorizontalLine(this, "AsianHigh", AsianHigh, Brushes.Green, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "AsianLow", AsianLow, Brushes.Green, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "AsianMiddle", AsianMiddle, Brushes.Green, DashStyleHelper.Dash, 1);
            }
        }
		protected override void OnBarUpdate()
		{
            FindAndDrawKeyLevels();



            if (DateTime.Now.Subtract(LastExecutionTime).TotalSeconds < 1)
            {
                return;
            }
            LastExecutionTime = DateTime.Now;
        }
	}
}
