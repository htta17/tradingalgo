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
    public class FVG : BarClosedBaseClass<FVGTradeAction, FVGTradeDetail>
	{
        public FVG() : base("FVG")
        { 
        }
        /// <summary>
        /// Khoảng cách tối thiểu giữa điểm cao (thấp) nhất của cây nến 1 và điểm thấp (cao) nhất của cây nến 3
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Khoảng cách",
            Description = "Khoảng cách tối thiểu giữa điểm cao (thấp) nhất của cây nến 1 và điểm thấp (cao) nhất của cây nến 3",
            Order = 3,
            GroupName = "Importants Configurations")]
        public double MinDistanceToDetectFVG { get; set; } = 0.5;
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = @"Fair Value Gap";
                Name = "FVG";
                BarsRequiredToTrade = 10;
                Target1InTicks = 40;
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();

                // Add data for trading
                AddDataSeries(BarsPeriodType.Minute, 5);

                currentTradeAction = FVGTradeAction.NoTrade;
            }
            else if (State == State.DataLoaded)
            {
                deadZoneSeries = new Series<double>(this);
            }
        }

        private Series<double> deadZoneSeries;

        protected override void OnBarUpdate()
        {
            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                var wae = FindWaddahAttarExplosion();

                if (TradingStatus == TradingStatus.Idle)
                {   
                    // Find the FVG value 
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade.FVGTradeAction}");

                    if ((shouldTrade.FVGTradeAction == FVGTradeAction.Buy || wae.HasBullVolume)
                        || (shouldTrade.FVGTradeAction == FVGTradeAction.Sell || wae.HasBearVolume))
                    {                        
                        EnterOrder(shouldTrade);

                        // Draw FVG using custom Rectangle method
                        DrawFVGBox(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    var shouldChangeVal = ShouldTrade();

                    // Nếu có vùng giá mới thì cập nhật
                    if (shouldChangeVal.FVGTradeAction == FVGTradeAction.NoTrade)
                    {
                        return;
                    }
                    
                    // Hủy lệnh cũ và order lệnh mới 
                    CancelAllPendingOrder();

                    EnterOrder(shouldChangeVal);

                    // Draw FVG using custom Rectangle method
                    DrawFVGBox(shouldChangeVal);
                }
                else if (TradingStatus == TradingStatus.OrderExists)
                { 
                    // Cập nhật lại target 2 
                }
            }
        }

        private void DrawFVGBox(FVGTradeDetail fVGTradeDetail)
        {
            Draw.Rectangle(this, $"FVG_{CurrentBar}_1", false, 0, fVGTradeDetail.StopLossPrice, -2, fVGTradeDetail.FilledPrice, Brushes.Transparent, Brushes.Red, 30);

            Draw.Rectangle(this, $"FVG_{CurrentBar}_2", false, 0, fVGTradeDetail.TargetProfitPrice, -2, fVGTradeDetail.FilledPrice, Brushes.Green, Brushes.Blue, 30);
        }

        private void EnterOrder(FVGTradeDetail fVGTradeDetail)
        {
            // Set global values
            currentTradeAction = fVGTradeDetail.FVGTradeAction;

            // Chưa cho move stop loss
            startMovingStoploss = false;

            var orderAction = fVGTradeDetail.FVGTradeAction == FVGTradeAction.Buy ? OrderAction.Buy : OrderAction.Sell;

            try
            {
                double priceToSet = GetSetPrice(fVGTradeDetail);
                filledPrice = priceToSet;

                var stopLossPrice = GetStopLossPrice(fVGTradeDetail, priceToSet);
                //var targetHalf = GetTargetPrice_Full(fVGTradeDetail, priceToSet);
                var targetFull = GetTargetPrice_Full(fVGTradeDetail, priceToSet);

                EnterOrderPure(priceToSet, targetFull, stopLossPrice,
                    StrategiesUtilities.SignalEntry_FVGFull, DefaultQuantity,
                    fVGTradeDetail.FVGTradeAction == FVGTradeAction.Buy,
                    fVGTradeDetail.FVGTradeAction == FVGTradeAction.Sell);

                
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }            
        }

        protected override double GetStopLossPrice(FVGTradeDetail tradeAction, double setPrice)
        {
            var stopLoss = tradeAction.FVGTradeAction == FVGTradeAction.Buy
                ? setPrice - (StopLossInTicks * TickSize)
                : setPrice + (StopLossInTicks * TickSize);
            return stopLoss;//  tradeAction.StopLossPrice;
        }

        protected override double GetTargetPrice_Half(FVGTradeDetail tradeDetail, double setPrice)
        {
            return currentTradeAction == FVGTradeAction.Buy 
                ? setPrice + (Target1InTicks * TickSize)
                : -setPrice - (Target1InTicks * TickSize);
        }

        protected override double GetTargetPrice_Full(FVGTradeDetail tradeDetail, double setPrice)
        {
            //return tradeDetail.TargetProfitPrice;

            return tradeDetail.FVGTradeAction == FVGTradeAction.Buy 
                ? setPrice + (Target2InTicks * TickSize)
                : setPrice - (Target2InTicks * TickSize);
        }

        double filledPrice = -1;
        DateTime filledTime = DateTime.Now;
        protected override FVGTradeDetail ShouldTrade()
        {
            // 
            if (High[2] < Low[0] &&  Low[0] - High[2] > MinDistanceToDetectFVG)
            {
                filledTime = Time[0];

                return new FVGTradeDetail
                {
                    FilledPrice = High[2],
                    FVGTradeAction = FVGTradeAction.Buy,
                    StopLossPrice = Low[2],
                    TargetProfitPrice = High[0]
                }; 
            }
            else if (Low[2] > High[0] && Low[2] - High[0] > MinDistanceToDetectFVG)
            {
                return new FVGTradeDetail
                {
                    FilledPrice = Low[2],
                    FVGTradeAction = FVGTradeAction.Sell,
                    StopLossPrice = High[2],
                    TargetProfitPrice = Low[0]
                };
            }
            return new FVGTradeDetail
            {
                FilledPrice = -1,
                FVGTradeAction = FVGTradeAction.NoTrade,
                StopLossPrice = -1,
                TargetProfitPrice = -1
            };
        }

        protected override double GetSetPrice(FVGTradeDetail tradeAction)
        {
            return tradeAction.FilledPrice; 
        }

        /// <summary>
        /// Tìm các giá trị của Waddah Attar Explosion ở khung 5 phút
        /// </summary>
        /// <returns></returns>
        private WAE_ValueSet FindWaddahAttarExplosion()
        {
            int sensitivity = 150;
            int fastLength = 20;
            int slowLength = 40;
            int channelLength = 20;
            double mult = 2.0;

            // WAE
            // Calculate Typical Price
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3.0;

            // Calculate True Range and store it in a Series
            double trueRange = Math.Max(High[0] - Low[0], Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
            deadZoneSeries[0] = trueRange; // Initialize the first value

            // Calculate smoothed ATR using EMA of the True Range Series
            double smoothedATR = EMA(deadZoneSeries, 100)[0];

            // Dead Zone
            double deadZone = smoothedATR * 3.7;

            // MACD Difference Calculation
            double fastEMA = EMA(Close, fastLength)[0];
            double slowEMA = EMA(Close, slowLength)[0];
            double prevFastEMA = EMA(Close, fastLength)[1];
            double prevSlowEMA = EMA(Close, slowLength)[1];

            double macd = fastEMA - slowEMA;
            double prevMacd = prevFastEMA - prevSlowEMA;
            double trendCalculation = (macd - prevMacd) * sensitivity;

            // Bollinger Bands Calculation
            double bbBasis = SMA(Close, channelLength)[0];
            double bbDev = mult * StdDev(Close, channelLength)[0];
            double bbUpperVal = bbBasis + bbDev;
            double bbLowerVal = bbBasis - bbDev;

            // Explosion Line
            double explosionValue = bbUpperVal - bbLowerVal;

            return new WAE_ValueSet
            {
                DeadZoneVal = deadZone,
                DownTrendVal = trendCalculation < 0 ? -trendCalculation : 0,
                ExplosionVal = explosionValue,
                UpTrendVal = trendCalculation >= 0 ? trendCalculation : 0
            };
        }
    }
}
