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
        }

        protected override void OnBarUpdate()
        {
            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                if (TradingStatus == TradingStatus.Idle)
                {
                    // Find the FVG value 
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade.FVGTradeAction}");

                    if (shouldTrade.FVGTradeAction != FVGTradeAction.NoTrade)
                    {
                        EnterOrder(shouldTrade);
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
                }
                else if (TradingStatus == TradingStatus.OrderExists)
                { 
                    // Cập nhật lại target 2 
                }
            }
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
                var targetHalf = GetTargetPrice_Half(fVGTradeDetail, priceToSet);
                var targetFull = GetTargetPrice_Full(fVGTradeDetail, priceToSet);

                EnterOrderPure(priceToSet, targetHalf, stopLossPrice,
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
            return tradeAction.StopLossPrice;
        }

        protected override double GetTargetPrice_Half(FVGTradeDetail tradeDetail, double setPrice)
        {
            return currentTradeAction == FVGTradeAction.Buy 
                ? setPrice + (Target1InTicks * TickSize)
                : -setPrice - (Target1InTicks * TickSize);
        }

        protected override double GetTargetPrice_Full(FVGTradeDetail tradeDetail, double setPrice)
        {
            return tradeDetail.TargetProfitPrice;
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
                    FilledPrice = High[2],
                    FVGTradeAction = FVGTradeAction.Buy,
                    StopLossPrice = Low[2],
                    TargetProfitPrice = High[0]
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
    }
}
