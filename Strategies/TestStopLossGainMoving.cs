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
using NinjaTrader.NinjaScript.MarketAnalyzerColumns;
using System.Xml.Linq;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public abstract class TestStopLossGainMoving : BarClosedATMBase<TradeAction>
	{
        protected override bool IsBuying
		{ 
			get 
			{
				return CurrentTradeAction == TradeAction.Buy_Trending;
			} 
		}

        protected override bool IsSelling
        {
            get
            {
                return CurrentTradeAction == TradeAction.Sell_Trending;
            }
        }

        protected override void OnStateChange()
		{
			base.OnStateChange();

			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "TestStopLossGainMoving";
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
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
			}
			else if (State == State.Configure)
			{
				StrategiesUtilities.CalculatePnL(this, Account, Print);
			}
		}

        protected override void SetDefaultProperties()
        {
			FullSizeATMName = "TestingMoving";

			HalfSizefATMName = "TestingMoving";

			AllowWriteLog = true;
        }

        protected override double GetSetPrice(TradeAction tradeAction)
        {
			return Close[0];
        }

        protected override void OnBarUpdate()
		{
			tradingStatus = CheckCurrentStatusBasedOnOrders();

			LocalPrint($"Trading status: {TradingStatus}");

			if (TradingStatus == TradingStatus.Idle)
			{
				var lastCandleIsRed = CandleUtilities.IsRedCandle(Close[0], Open[0]);
				if (lastCandleIsRed)
				{
					LocalPrint("Enter SELL");
					EnterOrder(TradeAction.Sell_Trending);
				}
				else
				{
                    LocalPrint("Enter BUY");
                    EnterOrder(TradeAction.Buy_Trending);
				}
			}			
		}
		

        protected override double GetStopLossPrice(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Half(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Full(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override TradeAction ShouldTrade()
        {
            throw new NotImplementedException();
        }
    }


    public class TestStopLossGainOrigin : Strategy
    {
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Enter the description for your new custom Strategy here.";
                Name = "TestStopLossGainMoving";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
            }
            else if (State == State.Configure)
            {
            }
        }

        string atmStrategyId = "";
        string orderId = "";
        string ATMName = "Phuong";
        protected override void OnBarUpdate()
        {
            //Add your custom strategy logic here.
            //var orders = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted); 

            // Find active order

            var pendingOrders = Account.Orders.Where(c => c.OrderState == OrderState.Accepted || c.OrderState == OrderState.Working);

            if (Position.Quantity == 0 && !pendingOrders.Any() && State == State.Realtime)
            {
                Print($"NO active position. Enter RANDOM now");

                var num = (new Random()).Next(0, 9);

                var action = Open[1] < Close[1] ? OrderAction.Buy : OrderAction.Sell;

                atmStrategyId = GetAtmStrategyUniqueId();
                orderId = GetAtmStrategyUniqueId();
                filledPrice = Close[0];

                AtmStrategyCreate(
                    action,
                    OrderType.Market,
                    0,
                    0,
                    TimeInForce.Day,
                    orderId,
                    ATMName,
                    atmStrategyId,
                    (atmCallbackErrorCode, atmCallBackId) =>
                    {
                        if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                        {
                            Print($"Enter {action} - strategyID: {atmStrategyId}");
                        }
                    });
            }

        }

        protected override void OnOrderUpdate(
            Order order,
            double limitPrice,
            double stopPrice,
            int quantity,
            int filled,
            double averageFillPrice,
            OrderState orderState,
            DateTime time,
            ErrorCode error,
            string comment)
        {
            Print($"Id: {order.Id}, limitPrice: {limitPrice:F2}, stop: {stopPrice:F2} {orderState}");
        }


        private OrderAction currentAction = OrderAction.Buy;
        private double filledPrice = -1;


        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            try
            {
                var updatedPrice = marketDataUpdate.Price;

                var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains("Stop")).ToList();
                var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains("Target")).ToList();

                var countTarget = targetOrders.Count();
                var countStop = stopOrders.Count();

                // Start of countTarget == 1 &&  countStop == 1
                if (countTarget == 1 && countStop == 1)
                {
                    var targetOrder = targetOrders.FirstOrDefault();
                    var stopOrder = stopOrders.FirstOrDefault();

                    if (currentAction == OrderAction.Buy)
                    {
                        // Move stop gain
                        if (targetOrder.LimitPrice > updatedPrice && targetOrder.LimitPrice - updatedPrice <= 5)
                        {
                            AtmStrategyChangeStopTarget(
                                targetOrder.LimitPrice + 5,
                                0,
                                targetOrder.Name,
                                atmStrategyId);

                            AtmStrategyChangeStopTarget(
                                0,
                                Math.Max(targetOrder.StopPrice, updatedPrice - 7),
                                targetOrder.Name,
                                atmStrategyId);

                            Print($"Current GAIN: {targetOrder.LimitPrice}, updatedPrice: {updatedPrice}. Move GAIN to {targetOrder.LimitPrice + 5} - BUY");
                        }
                    }
                    else if (currentAction == OrderAction.Sell)
                    {
                        // Move stop gain
                        if (updatedPrice - targetOrder.LimitPrice <= 5)
                        {
                            AtmStrategyChangeStopTarget(
                                targetOrder.LimitPrice - 5,
                                Math.Min(stopOrder.StopPrice, updatedPrice + 7),
                                targetOrder.Name,
                                atmStrategyId);

                            AtmStrategyChangeStopTarget(
                                targetOrder.LimitPrice - 5,
                                Math.Min(stopOrder.StopPrice, updatedPrice + 7),
                                targetOrder.Name,
                                atmStrategyId);

                            Print($"Move GAIN to {targetOrder.LimitPrice - 5} - SELL");
                        }
                    }
                }
                // End of countTarget == 1 &&  countStop == 1

                /*
				* Keep it blank intentionally
				*/
            }
            catch (Exception e)
            {
                Print(e.Message);
            }
        }
        /*
		* End of the class. Keep it blank intentionally.
		*/
    }
}
