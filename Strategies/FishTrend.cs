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
	public class FishTrend : BarClosedATMBase<TradeAction>
	{
        public FishTrend() : base("FISHTREND")
        { 
        }
        protected override TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        protected override bool IsBuying => throw new NotImplementedException();

        protected override bool IsSelling => throw new NotImplementedException();

        private double KeyLevel_15m_UP = -1;
        private double KeyLevel_15m_DOWN = -1;

        private double KeyLevel_5m_DOWN = -1;
        private double KeyLevel_5m_UP = -1;

        private double Previous_KeyLevel_5m_DOWN = -1;
        private double Previous_KeyLevel_5m_UP = -1;

        private EMA EMA46Indicator_5m { get; set; }
        private EMA EMA51Indicator_5m { get; set; }

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;
        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double lowPrice_15m = -1;
        protected double highPrice_15m = -1;
        protected double closePrice_15m = -1;
        protected double openPrice_15m = -1;

        protected int TradeCounter { get; set; } = 0;

        protected override void AddCustomDataSeries()
        {
            
        }

        protected override void AddCustomIndicators()
        {
            
        }

        protected override void OnStateChange()
		{           
            base.OnStateChange();
            if (State == State.SetDefaults)
            {
                Name = "FishTrend"; 
                Description = @"Use EMA46/51 khung 5 phút và các cây nến khung 5 phút";
                // Let not set Name here, each inheritted class will set by itself
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 2;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = Cbi.TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.IgnoreAllErrors;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;                
            }
            else if (State == State.Configure)
            {
                try
                {
                    var newsFromFile = GeneralUtilities.ReadNewsInfoFromFile(Print);

                    if (newsFromFile != string.Empty)
                    {
                        newsFromFile = $"{StrategiesUtilities.DefaultNewsTime},{newsFromFile}";
                        Print($"[NewsTime]: {newsFromFile}");
                    }
                    else // Nếu ngày hôm nay không có gì thì chỉ lấy thời gian mở, đóng cửa. 
                    {
                        newsFromFile = StrategiesUtilities.DefaultNewsTime;
                    }

                    NewsTimes = newsFromFile.Split(',').Select(c => int.Parse(c)).ToList();
                }
                catch (Exception e)
                {
                    Print($"[OnStateChange] - ERROR: " + e.Message);
                }

                // Add data series
                AddDataSeries(BarsPeriodType.Minute, 15);
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                EMA46Indicator_5m = EMA(46);
                EMA46Indicator_5m.Plots[0].Brush = Brushes.DarkOrange;

                EMA51Indicator_5m = EMA(51);
                EMA51Indicator_5m.Plots[0].Brush = Brushes.DeepSkyBlue;
                EMA51Indicator_5m.Plots[0].DashStyleHelper = DashStyleHelper.Dash;

                AddChartIndicator(EMA46Indicator_5m);
                AddChartIndicator(EMA51Indicator_5m);
            }    
            else if (State == State.Realtime)
            {
                try
                {
                    // Nếu có lệnh đang chờ thì cancel 
                    TransitionOrdersToLive();
                }
                catch (Exception e)
                {
                    LocalPrint("[OnStateChange] - ERROR" + e.Message);
                }
            }
        }

		protected override void OnBarUpdate()
		{
            if (BarsInProgress == 0)
            {
                // Current View --> return
                return;
            }

            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();

            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }            

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 15) //15 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                highPrice_15m = High[0];
                lowPrice_15m = Low[0];
                openPrice_15m = Open[0];
                closePrice_15m = Close[0];

                if (highPrice_15m > EMA46Indicator_5m.Value[0] && highPrice_15m > EMA51Indicator_5m.Value[0] &&
                    lowPrice_15m < EMA46Indicator_5m.Value[0] && lowPrice_15m < EMA51Indicator_5m.Value[0])
                {
                    // Restart couter
                    TradeCounter = 0;

                    KeyLevel_15m_UP = highPrice_15m; 
                    KeyLevel_15m_DOWN = lowPrice_15m;
                }    

                BasicActionForTrading(TimeFrameToTrade.FiveMinutes);
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                highPrice_15m = High[0];
                lowPrice_15m = Low[0];
                openPrice_15m = Open[0];
                closePrice_15m = Close[0];

                if (highPrice_15m > EMA46Indicator_5m.Value[0] && highPrice_15m > EMA51Indicator_5m.Value[0] &&
                    lowPrice_15m < EMA46Indicator_5m.Value[0] && lowPrice_15m < EMA51Indicator_5m.Value[0])
                {
                    // Restart couter
                    TradeCounter = 0;

                    KeyLevel_5m_UP = highPrice_15m;
                    KeyLevel_5m_DOWN = lowPrice_15m;
                }

                BasicActionForTrading(TimeFrameToTrade.FiveMinutes);                
            }            
        }

        protected override void BasicActionForTrading(TimeFrameToTrade timeFrameToTrade)
        {
            if (timeFrameToTrade != TimeFrameToTrade.FiveMinutes)
            {
                return;
            }
        }

        protected override double GetSetPrice(TradeAction tradeAction, AtmStrategy additionalInfo)
        {
            throw new NotImplementedException();
        }

        protected override TradeAction ShouldTrade()
        {
            throw new NotImplementedException();
        }
    }
}
