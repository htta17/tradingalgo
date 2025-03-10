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
using NinjaTrader.CQG.ProtoBuf;
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class ADXBollinger : BarClosedATMBase<ADXBollingerAction>
    {
        public ADXBollinger() : base("TIGER")
        {
            
        }

        #region Parameters
        /// <summary>
        /// ADX khung 5 phút &lt; [ADXToEnterOrder] thì set lệnh LIMIT
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enter order  ADX < ?:", Order = 2, GroupName = "Trading Parameters")]
        public int ADXToEnterOrder { get; set; }

        /// <summary>
        /// ADX khung 5 phút > [ADXToCancelOrder] thì cancel lệnh LIMIT
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Vào lệnh nếu ADX < ?:", Order = 2, GroupName = "Trading Parameters")]
        public int ADXToCancelOrder { get; set; }
        #endregion       

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Tiger [ADX + Bollinger (Reverse)]";
            Description = "";

            tradingStatus = TradingStatus.Idle;

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";

            ADXToEnterOrder = 20;
            ADXToCancelOrder = 24;

            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyADX.txt");
        }
        
        private Bollinger bollinger1Indicator_5m { get; set; }
        private Bollinger bollinger2Indicator_5m { get; set; }
        private ADX adxIndicator_5m { get; set; }

        protected override bool IsBuying
        { 
            get 
            { 
                return CurrentTradeAction == ADXBollingerAction.SetBuyOrder; 
            } 
        }

        protected override bool IsSelling
        {
            get
            {
                return CurrentTradeAction == ADXBollingerAction.SetSellOrder;
            }
        }

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double adx_5m = -1;

        protected double upperBB_5m = -1;
        protected double lowerBB_5m = -1;
        protected double middleBB_5m = -1;

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;

        protected override void OnStateChange()
		{
			base.OnStateChange();

            if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                FullSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(FullSizeATMName).AtmStrategy;

                HalfSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(HalfSizefATMName).AtmStrategy;
            }
            else if (State == State.DataLoaded)
            {
                bollinger1Indicator_5m = Bollinger(1, 20);
                bollinger1Indicator_5m.Plots[0].Brush = bollinger1Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                bollinger2Indicator_5m = Bollinger(2, 20);
                bollinger2Indicator_5m.Plots[0].Brush = bollinger2Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                adxIndicator_5m = ADX(14);                

                AddChartIndicator(bollinger1Indicator_5m);
                AddChartIndicator(bollinger2Indicator_5m);                

                AddChartIndicator(adxIndicator_5m);
            }
            else if (State == State.Realtime)
            {
            }
        }  

        protected override void OnBarUpdate()
		{
            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();

            //Add your custom strategy logic here.
            var passTradeCondition = CheckingTradeCondition(ValidateType.MaxDayGainLoss);
            if (!passTradeCondition)
            {
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                openPrice_5m = Open[0];
                closePrice_5m = Close[0];

                adx_5m = adxIndicator_5m.Value[0];

                upperBB_5m = bollinger1Indicator_5m.Upper[0];
                lowerBB_5m = bollinger1Indicator_5m.Lower[0];
                middleBB_5m = bollinger1Indicator_5m.Middle[0];

                upperStd2BB_5m = bollinger2Indicator_5m.Upper[0];
                lowerStd2BB_5m = bollinger2Indicator_5m.Lower[0];

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if (shouldTrade == ADXBollingerAction.SetBuyOrder || shouldTrade == ADXBollingerAction.SetSellOrder)
                    {
                        // Enter Order
                        EnterOrder(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    // Kiểm tra các điều kiện để cancel lệnh

                    if (adx_5m > ADXToCancelOrder)
                    {
                        LocalPrint($"Price is greater than Bollinger middle, cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }
                    else if (CurrentTradeAction == ADXBollingerAction.SetBuyOrder && lowPrice_5m > middleBB_5m)
                    {
                        LocalPrint($"Price is greater than Bollinger middle, cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }
                    else if (CurrentTradeAction == ADXBollingerAction.SetSellOrder && highPrice_5m < middleBB_5m)
                    {
                        LocalPrint($"Price is smaller than Bollinger middle, Cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }
                    else 
                    {
                        var shouldTrade = ShouldTrade();

                        // Xem điều kiện có bị thay đổi gì không? 
                        if (shouldTrade == ADXBollingerAction.NoTrade)
                        {
                            // Do nothing, do việc cancel xảy ra khi adx_5m > [ADXToCancelOrder]
                        }
                        else
                        {
                            if (shouldTrade == CurrentTradeAction)
                            {
                                var newPrice = GetSetPrice(shouldTrade);

                                var stopLossPrice = GetStopLossPrice(shouldTrade, newPrice);                                

                                var targetPrice_Full = GetTargetPrice_Full(shouldTrade, newPrice);

                                LocalPrint($"Update entry price to {newPrice}");

                                UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full);
                            }
                            else
                            {
                                CancelAllPendingOrder(); 

                                EnterOrder(shouldTrade);
                            }
                        }
                    }
                }
                else if (TradingStatus == TradingStatus.OrderExists)
                {
                    
                }
            }
        }
        protected override double GetSetPrice(ADXBollingerAction tradeAction)
        {
            if (tradeAction == ADXBollingerAction.SetBuyOrder)
            {
                return lowerStd2BB_5m; 
            }
            else if (tradeAction == ADXBollingerAction.SetSellOrder)
            {
                return upperStd2BB_5m;
            }
            return middleBB_5m;
        }

        protected override double GetStopLossPrice(ADXBollingerAction tradeAction, double setPrice)
        {
            var stopLoss = StopLossInTicks * TickSize;

            return tradeAction == ADXBollingerAction.SetBuyOrder
                ? setPrice - stopLoss
                : setPrice + stopLoss;
        }

        protected override double GetTargetPrice_Half(ADXBollingerAction tradeAction, double setPrice)
        {
            var target1 = TickSize * Target1InTicks; 

            return tradeAction == ADXBollingerAction.SetBuyOrder
                ? setPrice + target1
                : setPrice - target1; 
        }

        protected override double GetTargetPrice_Full(ADXBollingerAction tradeAction, double setPrice)
        {
            var target2 = TickSize * Target2InTicks;

            return tradeAction == ADXBollingerAction.SetBuyOrder
                ? setPrice + target2
                : setPrice - target2;
        }

        protected override ADXBollingerAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return ADXBollingerAction.NoTrade;
            }

            if (adx_5m < ADXToEnterOrder) 
            {
                if (lowPrice_5m > middleBB_5m)
                {
                    return ADXBollingerAction.SetSellOrder;
                }
                else if (highPrice_5m < middleBB_5m)
                {
                    return ADXBollingerAction.SetBuyOrder;
                }
            } 

            return ADXBollingerAction.NoTrade; 
        }
    }
}
