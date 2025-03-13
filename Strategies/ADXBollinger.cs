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
        public double ADXToEnterOrder { get; set; }

        /// <summary>
        /// ADX khung 5 phút > [ADXToCancelOrder] thì cancel lệnh LIMIT
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Vào lệnh nếu ADX < ?:", Order = 2, GroupName = "Trading Parameters")]
        public double ADXToCancelOrder { get; set; }
        #endregion       

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Tiger [ADX + Bollinger (Reverse)]";
            Description = "";

            tradingStatus = TradingStatus.Idle;

            FullSizeATMName = "Tiger_Default_4cts";
            HalfSizefATMName = "Tiger_Default_2cts";

            ADXToEnterOrder = 19.5;
            ADXToCancelOrder = 22;

            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyADX.txt");

            MaximumDailyLoss = 260;
            DailyTargetProfit = 600;
        }
        
        private Bollinger bollinger1Indicator_5m { get; set; }
        private Bollinger bollinger2Indicator_5m { get; set; }
        //private ADX adxIndicator_5m { get; set; }

        private ADXandDI ADXandDIIndicator_5m { get; set; }        

        protected override bool IsBuying => CurrentTradeAction == ADXBollingerAction.SetBuyOrder;

        protected override bool IsSelling => CurrentTradeAction == ADXBollingerAction.SetSellOrder;

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double adx_5m = -1;
        protected double diPlus_5m = -1;
        protected double diMinus_5m = -1;

        protected double upperBB_5m = -1;
        protected double lowerBB_5m = -1;
        protected double middleBB_5m = -1;

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;
        protected override void OnStateChange()
		{
			base.OnStateChange();

            if (State == State.DataLoaded)
            {
                bollinger1Indicator_5m = Bollinger(1, 20);
                bollinger1Indicator_5m.Plots[0].Brush = bollinger1Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                bollinger2Indicator_5m = Bollinger(2, 20);
                bollinger2Indicator_5m.Plots[0].Brush = bollinger2Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                //adxIndicator_5m = ADX(14);
                ADXandDIIndicator_5m = ADXandDI(14, ADXToEnterOrder, ADXToCancelOrder);

                AddChartIndicator(bollinger1Indicator_5m);
                AddChartIndicator(bollinger2Indicator_5m);

                //AddChartIndicator(adxIndicator_5m);
                AddChartIndicator(ADXandDIIndicator_5m);
            }
        }

        private DateTime barUpdateExecutionTime = DateTime.MinValue;
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

            base.OnBarUpdate();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                if (DateTime.Now.Subtract(barUpdateExecutionTime).TotalSeconds < 1) // Avoid duplicated
                {
                    return;
                }
                barUpdateExecutionTime = DateTime.Now;

                StrategiesUtilities.CalculatePnL(this, Account, Print);

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();                    

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
                                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL();

                                var newPrice = GetSetPrice(shouldTrade, atmStrategy);
                                
                                var stopLossPrice = GetStopLossPrice(shouldTrade, newPrice, atmStrategy);

                                var targetPrice_Full = GetTargetPrice_Full(shouldTrade, newPrice, atmStrategy);

                                var targetPrice_Half = GetTargetPrice_Half(shouldTrade, newPrice, atmStrategy);

                                LocalPrint($"Update entry price to {newPrice:N2}.");

                                UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);
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
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                openPrice_5m = Open[0];
                closePrice_5m = Close[0];

                adx_5m = ADXandDIIndicator_5m.Value[0];

                diPlus_5m = ADXandDIIndicator_5m.Values[1][0];
                diMinus_5m = ADXandDIIndicator_5m.Values[2][0];

                upperBB_5m = bollinger1Indicator_5m.Upper[0];
                lowerBB_5m = bollinger1Indicator_5m.Lower[0];
                middleBB_5m = bollinger1Indicator_5m.Middle[0];

                upperStd2BB_5m = bollinger2Indicator_5m.Upper[0];
                lowerStd2BB_5m = bollinger2Indicator_5m.Lower[0];

                LocalPrint($"Update 5 minutes values: adx_5m {adx_5m:N2}, upperStd2BB_5m: {upperStd2BB_5m:N2}, lowerStd2BB_5m: {lowerStd2BB_5m:N2}.");

                CurrentBarIndex_5m = CurrentBar;

                /*
                if (TradingStatus == TradingStatus.OrderExists) 
                {
                    if (IsBuying && TargetPrice_Half > upperBB_5m + 2)
                    {

                    }
                    else if (IsSelling && TargetPrice_Half < lowerBB_5m - 2)
                    { 
                    }
                }
                */
            }
        }
        protected override double GetSetPrice(ADXBollingerAction tradeAction, AtmStrategy atmStrategy)
        {
            if (tradeAction == ADXBollingerAction.SetBuyOrder)
            {
                return StrategiesUtilities.RoundPrice(lowerStd2BB_5m); 
            }
            else if (tradeAction == ADXBollingerAction.SetSellOrder)
            {
                return StrategiesUtilities.RoundPrice(upperStd2BB_5m);
            }
            return StrategiesUtilities.RoundPrice(middleBB_5m);
        }

        protected override ADXBollingerAction ShouldTrade()
        {
            var answer = ADXBollingerAction.NoTrade;

            if (adx_5m < ADXToEnterOrder) 
            {
                if (lowPrice_5m > middleBB_5m)
                {
                    answer = ADXBollingerAction.SetSellOrder;
                }
                else if (highPrice_5m < middleBB_5m)
                {
                    answer = ADXBollingerAction.SetBuyOrder;
                }
            }

            LocalPrint($"[ShouldTrade]: adx_5m: {adx_5m:N2}, ADXToEnterOrder: {ADXToEnterOrder}, lowPrice_5m: {lowPrice_5m:N2}, middleBB_5m: {middleBB_5m:N2}, highPrice_5m: {highPrice_5m}, ans: {answer}. ");

            return answer; 
        }
    }
}
