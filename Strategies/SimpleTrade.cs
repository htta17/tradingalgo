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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class SimpleTrade : Strategy
	{
        private const int DEMA_Period = 9;
        private const int FiveMinutes_Period = 14;

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

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double currentDEMA_5m = -1;
        protected double lastDEMA_5m = -1;

        // Volume 
        protected double volume_5m = -1;
        protected double avgEMAVolume_5m = -1;
        protected double volumeBuy_5m = -1;
        protected double volumeSell_5m = -1;
        // ADX
        protected double adx_5m = -1;
        protected double plusDI_5m = -1;
        protected double minusDI_5m = -1;

        protected bool crossAbove_5m = false;
        protected bool crossBelow_5m = false;
        #endregion

        protected TradeAction currentTradeAction = TradeAction.NoTrade;        
        protected double filledPrice = -1;

        private int CountOrders = 0;

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop loss (Ticks):",
            Order = 8,
            GroupName = "Parameters")]
        public int StopLossInTicks { get; set; } = 120; // 25 points for MNQ

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "SimpleTrade";
				Calculate									= Calculate.OnBarClose;
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
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration	= true;
			}
			else if (State == State.Configure)
			{
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
		}
        
		protected virtual TradeAction ShouldTrade()
        {
            if (adx_5m > 25 ) // Nếu là xu hướng mạnh và có volume
            {
                // Check xem là xu hướng gì
                if (plusDI_5m > minusDI_5m && crossAbove_5m)
                {
                    Print($"Found BUY signal (Trending) - adx_5m: {adx_5m:N2}, volume_5m: {volume_5m:N2}, " +
                        $"avgEMAVolume_5m: {avgEMAVolume_5m:N2}, " +
                        $"plusDI_5m: {plusDI_5m:N2}, " +
                        $"minusDI_5m: {minusDI_5m:N2}");

                    return TradeAction.Buy_Trending;
                }
                else if (plusDI_5m < minusDI_5m && crossBelow_5m)
                {
                    Print($"Found SELL signal (Trending) - adx_5m: {adx_5m:N2}, volume_5m: {volume_5m:N2}, " +
                        $"avgEMAVolume_5m: {avgEMAVolume_5m:N2}, " +
                        $"plusDI_5m: {plusDI_5m:N2}, " +
                        $"minusDI_5m: {minusDI_5m:N2}");

                    return TradeAction.Sell_Trending;
                }
            }

            return TradeAction.NoTrade;
        }

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            //base.OnOrderUpdate(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, comment);

            if (orderState == OrderState.Filled || orderState == OrderState.Cancelled)
            {
                CountOrders--;
            }
            else if (orderState == OrderState.Working)
            {
                CountOrders++;                
            }

            Print($"OnOrderUpdate - {order.Id} - {order.Name}, orderType: {order.OrderType}, orderState: {orderState}, " +
                $"limitPrice: {limitPrice:N2}, stop: {stopPrice:N2}, status: CountOrders: {CountOrders} ");

        }

        protected virtual double GetSetPrice(TradeAction tradeAction)
        {
            return Math.Round((ema29_1m + ema51_1m) / 2.0 * 4, MidpointRounding.AwayFromZero) / 4.0; ;
        }

        Order CurrentOrder = null;

        protected virtual void EnterOrder(TradeAction tradeAction)
        {
            var action = tradeAction == TradeAction.Buy_Trending || tradeAction == TradeAction.Buy_Reversal ? OrderAction.Buy : OrderAction.Sell;                        

            double priceToSet = GetSetPrice(tradeAction);

            Print($"Attempting to place order: TradeAction = {tradeAction}, Price = {priceToSet}");

            if (action == OrderAction.Buy)
            {
                CurrentOrder = EnterLongLimit(2, true, 2, priceToSet, "H");

                Print($"Filled {(CurrentOrder != null ? "successful" : "failed")}");
            }
            else
            {
                CurrentOrder = EnterShortLimit(2, true, 2, priceToSet, "H");

                Print($"Filled {(CurrentOrder != null ? "successful" : "failed")}");
            }

            SetStopLoss("H", CalculationMode.Ticks, StopLossInTicks, false);
            SetProfitTarget("H", CalculationMode.Ticks, StopLossInTicks);
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            //base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);

            Print($"Execution update: {execution.Order.Name}, Filled: {execution.Quantity}, AvgPrice: {execution.Price}");
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
            {
                Print($"Not enough bars to trade: {CurrentBar}/{BarsRequiredToTrade}");
                return;
            }

            //Add your custom strategy logic here.

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                Print($"{Time[0]} - 1 minute frame - {CountOrders} orders ");
                // Cập nhật EMA29 và EMA51	
                ema21_1m = EMA(21).Value[0];
                ema29_1m = EMA(29).Value[0];
                ema51_1m = EMA(51).Value[0];
                ema120_1m = EMA(120).Value[0];

                currentPrice = Close[0];

                if (CountOrders == 0)
                {
                    var shouldTrade = ShouldTrade();

                    if (shouldTrade != TradeAction.NoTrade)
                    {
                        EnterOrder(shouldTrade);
                    }
                }                
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                Print($"{Time[0]} - 5 minute frame - {CountOrders} orders - Start");
                var bollinger = Bollinger(1, 20);
                var bollingerStd2 = Bollinger(2, 20);

                volume_5m = Volume[0];
                avgEMAVolume_5m = EMA(Volume, FiveMinutes_Period)[0];
                adx_5m = ADX(FiveMinutes_Period).Value[0];
                plusDI_5m = DM(FiveMinutes_Period).DiPlus[0];
                minusDI_5m = DM(FiveMinutes_Period).DiMinus[0];

                crossAbove_5m = CrossAbove(DM(FiveMinutes_Period).DiPlus, DM(FiveMinutes_Period).DiMinus, 1);
                crossBelow_5m = CrossBelow(DM(FiveMinutes_Period).DiPlus, DM(FiveMinutes_Period).DiMinus, 1);

                upperBB_5m = bollinger.Upper[0];
                lowerBB_5m = bollinger.Lower[0];
                middleBB_5m = bollinger.Middle[0];

                upperStd2BB_5m = bollingerStd2.Upper[0];
                lowerStd2BB_5m = bollingerStd2.Lower[0];

                lowPrice_5m = Low[0];
                highPrice_5m = High[0];

                currentDEMA_5m = DEMA(DEMA_Period).Value[0];
                lastDEMA_5m = DEMA(DEMA_Period).Value[1];
                currentPrice = Close[0];                
            }

        }
	}
}
