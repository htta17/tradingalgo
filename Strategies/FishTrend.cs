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

        protected int TradeCounter { get; set; } = 0;

        protected double currentEMA46_5m = -1;
        protected double currentEMA51_5m = -1;


        protected override void AddCustomDataSeries()
        {
            // Add data series
            AddDataSeries(BarsPeriodType.Minute, 5);
            AddDataSeries(BarsPeriodType.Minute, 15);            
        }

        protected override void AddCustomIndicators()
        {
            EMA46Indicator_5m = EMA(46);
            EMA46Indicator_5m.Plots[0].Brush = Brushes.DarkOrange;

            EMA51Indicator_5m = EMA(51);
            EMA51Indicator_5m.Plots[0].Brush = Brushes.DeepSkyBlue;
            EMA51Indicator_5m.Plots[0].DashStyleHelper = DashStyleHelper.Dash;

            AddChartIndicator(EMA46Indicator_5m);
            AddChartIndicator(EMA51Indicator_5m);
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "FishTrend";
            Description = @"Use EMA46/51 khung 5 phút và các cây nến khung 5 phút";

            StartDayTradeTime = new TimeSpan(6, 59, 0); // 6:59:00 am 
            EndDayTradeTime = new TimeSpan(10, 30, 0); // 2:00:00 pm
        }

        private double GetMaxFromValues(params double[] values)
        { 
            return values.Max(x => x);
        }

        private double GetMinFromValues(params double[] values)
        {
            return values.Min(x => x);
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
                /*
                 * Điều kiện để chuyển Status sang Waiting. 
                 *  1. Cây nến 15 phút HIỆN TẠI có High > EMA46 (khung 5 phút) và High > EMA51 (khung 5 phút)
                 *  2. Cây nến 15 phút HIỆN TẠI có Low < EMA46 (khung 5 phút) và Low < EMA51 (khung 5 phút)
                 *  
                 *  3. Cây nến 15 phút TRƯỚC có [High & Low > EMA46] (khung 5 phút) và [High & Low > EMA51] (khung 5 phút)
                 *  4. Hoặc cây nến 15 phút TRƯỚC có [High & Low < EMA46] (khung 5 phút) và [High & Low < EMA51] (khung 5 phút)
                 */

                double highPrice_15m = High[0];
                double lowPrice_15m = Low[0];
                double openPrice_15m = Open[0];
                double closePrice_15m = Close[0];

                double pre_highPrice_15m = High[1];
                double pre_lowPrice_15m = Low[1];
                double pre_openPrice_15m = Open[1];
                double pre_closePrice_15m = Close[1];

                // EMA46Indicator_5m.Value[0], EMA46Indicator_5m.Value[1], EMA46Indicator_5m.Value[2] là 3 giá trị của EMA 46 khung 5 phút 
                //      dùng để so với cây nến 15 phút hiện tại 

                var emaValues_Current = new double[] 
                {
                    //EMA46
                    EMA46Indicator_5m.Value[0],
                    EMA46Indicator_5m.Value[1],
                    EMA46Indicator_5m.Value[2],

                    //EMA51
                    EMA51Indicator_5m.Value[0],
                    EMA51Indicator_5m.Value[1],
                    EMA51Indicator_5m.Value[2]
                };

                var emaValues_Previous = new double[]
                {
                    //EMA46
                    EMA46Indicator_5m.Value[3],
                    EMA46Indicator_5m.Value[4],
                    EMA46Indicator_5m.Value[5],

                    //EMA51
                    EMA51Indicator_5m.Value[3],
                    EMA51Indicator_5m.Value[4],
                    EMA51Indicator_5m.Value[5]
                };

                var maxEma_Current = GetMaxFromValues(emaValues_Current);
                var minEma_Current = GetMinFromValues(emaValues_Current);

                var maxEma_Previous = GetMaxFromValues(emaValues_Previous);
                var minEma_Previous = GetMinFromValues(emaValues_Previous);

                var shouldSetRange =
                    // Current - cross ema
                    highPrice_15m > maxEma_Current &&
                    lowPrice_15m < minEma_Current &&                    
                    // Previous - all above or all below
                    ((pre_highPrice_15m < minEma_Previous && pre_lowPrice_15m < minEma_Previous) || (pre_highPrice_15m > maxEma_Previous && pre_lowPrice_15m > maxEma_Previous));

                if (shouldSetRange)
                {
                    // Restart couter
                    TradeCounter = 0;

                    KeyLevel_15m_UP = highPrice_15m; 
                    KeyLevel_15m_DOWN = lowPrice_15m;
                }                
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                double highPrice_15m = High[0];
                double lowPrice_15m = Low[0];
                double openPrice_15m = Open[0];
                double closePrice_15m = Close[0];

                if (highPrice_15m > EMA46Indicator_5m.Value[0] && highPrice_15m > EMA51Indicator_5m.Value[0] &&
                    lowPrice_15m < EMA46Indicator_5m.Value[0] && lowPrice_15m < EMA51Indicator_5m.Value[0])
                {
                    // Restart couter
                    TradeCounter = 0;

                    KeyLevel_5m_UP = highPrice_15m;
                    KeyLevel_5m_DOWN = lowPrice_15m;
                }
                
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
