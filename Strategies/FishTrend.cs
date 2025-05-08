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
using NinjaTrader.NinjaScript.SuperDomColumns;
using System.Xml.Linq;
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

        private double KeyLevel_5m_HIGH = -1;
        private double KeyLevel_5m_LOW = -1;

        private EMA EMA46Indicator_5m { get; set; }
        private EMA EMA50Indicator_5m { get; set; }

        private int Last5mBarTouchEMA50 { get; set; } = -1; 

        protected int TradeCounter { get; set; } = 0;

        protected double currentEMA46_5m = -1;
        protected double currentEMA50_5m = -1;        
        protected override void AddCustomDataSeries()
        {
            // Add data series
            AddDataSeries(BarsPeriodType.Minute, 15);
            AddDataSeries(BarsPeriodType.Minute, 5);
            AddDataSeries(BarsPeriodType.Minute, 1);
        }
        protected override void AddCustomIndicators()
        {
            
        }

        protected override void OnStateChange_DataLoaded()
        {
            EMA46Indicator_5m = EMA(BarsArray[2], 46);
            EMA46Indicator_5m.Plots[0].Brush = Brushes.Black;

            EMA50Indicator_5m = EMA(BarsArray[2], 50);
            EMA50Indicator_5m.Plots[0].Brush = Brushes.Red;
            EMA50Indicator_5m.Plots[0].DashStyleHelper = DashStyleHelper.Dash;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "FishTrend";
            Description = @"Use EMA46/51 khung 5 phút và các cây nến khung 5 phút";

            StartDayTradeTime = new TimeSpan(6, 59, 0); // 6:59:00 am 
            EndDayTradeTime = new TimeSpan(10, 30, 0); // 2:00:00 pm

            AddPlot(Brushes.Black, "EMA46_5m");
            AddPlot(Brushes.Red, "EMA50_5m");
        }       

        protected override void OnBarUpdate()
		{            
            // Hiển thị indicators (Plot)
            try
            {
                Values[0][0] = EMA46Indicator_5m.Value[0];
                Values[1][0] = EMA50Indicator_5m.Value[0];
            }
            catch (Exception ex)
            {
                LocalPrint("[OnBarUpdate]: ERROR:" + ex.Message);
            }            

            if (BarsInProgress == 0 || CurrentBar < 60)                    
            {
                // Current View --> return
                return;
            }

            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1)
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                LocalPrint($"LOW KEY: {KeyLevel_5m_LOW:N2}, HIGH: {KeyLevel_5m_HIGH:N2}");
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                double highPrice_5m = High[0];
                double lowPrice_5m = Low[0];
                double openPrice_5m = Open[0];
                double closePrice_5m = Close[0];

                var maxEma_Current = StrategiesUtilities.MaxOfArray(EMA46Indicator_5m.Value[0], EMA50Indicator_5m.Value[0]);
                var minEma_Current = StrategiesUtilities.MinOfArray(EMA46Indicator_5m.Value[0], EMA50Indicator_5m.Value[0]);

                if (highPrice_5m > maxEma_Current && lowPrice_5m < minEma_Current)
                {
                    if (Last5mBarTouchEMA50 != CurrentBar - 1)
                    {
                        LocalPrint($"[CONFIRM] Found new range to trade. Low: {lowPrice_5m:N2}, High: {highPrice_5m:N2}");

                        KeyLevel_5m_HIGH = highPrice_5m;
                        KeyLevel_5m_LOW = lowPrice_5m;

                        Draw.Line(this, $"5m_HIGH_{CurrentBar}", false, 1, highPrice_5m, -1, highPrice_5m, Brushes.Green, DashStyleHelper.Solid, 2);
                        Draw.Line(this, $"5m_LOW_{CurrentBar}", false, 1, lowPrice_5m, -1, lowPrice_5m, Brushes.Green, DashStyleHelper.Solid, 2);
                        Draw.Line(this, $"5m_VERTICAL_{CurrentBar}", false, 0, lowPrice_5m, 0, highPrice_5m, Brushes.Green, DashStyleHelper.Solid, 2);

                        // Draw current line 
                        Draw.HorizontalLine(this, $"5m_HIGH_Current", highPrice_5m, Brushes.Orange, DashStyleHelper.Dot, 2);
                        Draw.HorizontalLine(this, $"5m_LOW_Current", lowPrice_5m, Brushes.Orange, DashStyleHelper.Dot, 2);

                        Last5mBarTouchEMA50 = CurrentBar; 
                    }
                }
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 15) //15 minute
            {

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

        protected override void BasicActionForTrading(TimeFrameToTrade timeFrameToTrade)
        {
            throw new NotImplementedException();
        }
    }
}
