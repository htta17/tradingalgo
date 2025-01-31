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

        /// <summary>
        /// Thời gian có news trong ngày
        /// </summary>
        private List<int> NewsTimes = new List<int>();

        #region 1 minute values
        protected double ema21_1m = -1;
        protected double ema29_1m = -1;
        protected double ema51_1m = -1;
        protected double ema120_1m = -1;
        protected double currentPrice = -1;
        #endregion

        #region 5 minutes values 
        protected double upperBB_5m = -1;
        protected double lowerBB_5m = -1;
        protected double middleBB_5m = -1;

        protected double lastUpperBB_5m = -1;
        protected double lastLowerBB_5m = -1;

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double currentDEMA_5m = -1;
        protected double lastDEMA_5m = -1;

        protected int barIndex_5m = 0;

        // Volume 
        protected double volume_5m = -1;
        protected double avgEMAVolume_5m = -1;
        protected double volumeBuy_5m = -1;
        protected double volumeSell_5m = -1;
        // ADX
        protected double adx_5m = -1;
        protected double plusDI_5m = -1;
        protected double minusDI_5m = -1;

        // WAE Values 
        protected double waeDeadVal_5m = -1;
        protected double waeExplosion_5m = -1;
        protected double waeUptrend_5m = -1;
        protected double waeDowntrend_5m = -1;

        private Series<double> deadZoneSeries;
        #endregion

        #region Configurations 
        [NinjaScriptProperty]
        [Display(Name = "Thời gian ra news (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
        public string NewsTimeInput { get; set; } = "0830,0500";
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

                NewsTimeInput = "0830,0500";
            }
			else if (State == State.Configure)
			{
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                StrategiesUtilities.CalculatePnL(this, Account, Print);

                try
                {
                    NewsTimes = NewsTimeInput.Split(',').Select(c => int.Parse(c)).ToList();
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }
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

            // OnPriceChange sẽ chạy quá nhiều, kiểm tra để 1 giây thì chạy 1 lần
            if (DateTime.Now.Subtract(LastExecutionTime).TotalSeconds < 1)
            {
                return;
            }
            LastExecutionTime = DateTime.Now;




        }
	}
}
