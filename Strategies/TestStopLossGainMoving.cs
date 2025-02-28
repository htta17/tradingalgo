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
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public abstract class TestStopLossGainMoving : Strategy
	{
		protected override void OnStateChange()
		{
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
			}
		}

		string atmStrategyId = "";
		string orderId = "";
		string ATMName = "Rooster_Default_2cts";

        const string SignalEntry1 = "Entry-MiddleBB";

        

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            Print($"[OnPositionUpdate]");
            base.OnPositionUpdate(position, averagePrice, quantity, marketPosition);
        }

        protected override void OnBarUpdate()
		{
			//Add your custom strategy logic here.
			var orders = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted); 
			
			// Find active order
			
			var pendingOrders = Account.Orders.Where(c => c.OrderState == OrderState.Accepted || c.OrderState == OrderState.Working);			
			
			if (Position.Quantity == 0 && !pendingOrders.Any() &&  State == State.Realtime)
			{
				Print($"NO active position. Enter RANDOM now");	
				
				var num = (new Random()).Next(0,9); 
				
				var action = Open[1] < Close[1] ? OrderAction.Buy : OrderAction.Sell;

                filledPrice = Close[0];
                
				atmStrategyId = GetAtmStrategyUniqueId();
				orderId = GetAtmStrategyUniqueId();				
				
				AtmStrategyCreate (
					action, 
					OrderType.Limit,
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
				     		Print($"Enter {action} - strategyID: {atmStrategyId}, orders: {Account.Orders.Where(c => c.OrderState == OrderState.Accepted || c.OrderState == OrderState.Working)}");


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
	   		Print($"[OnOrderUpdate] - Id: {order.Id}, limitPrice: {limitPrice:F2}, stop: {stopPrice:F2} {orderState}");		
	    }
		
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            Print($"[OnExecutionUpdate] ");

            base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);
        }


        private OrderAction currentAction = OrderAction.Buy;
		private double filledPrice = -1; 	
		
		protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
		{
			try 
			{
				var updatedPrice = marketDataUpdate.Price;

				if (updatedPrice < 100)
				{
					return;
				}
				
				var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains("Stop")).ToList();
				var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains("target")).ToList();
				
				var countTarget = targetOrders.Count(); 
				var countStop = stopOrders.Count();				
				
				// Start of countTarget == 1 &&  countStop == 1
				if (countTarget == 1 &&  countStop == 1) 
				{
					var targetOrder = targetOrders.FirstOrDefault(); 
					var stopOrder = stopOrders.FirstOrDefault();

                    //Print($"countTarget: {countTarget}, countStop: {countTarget}, gain: {targetOrder.LimitPrice}, stop: {stopOrder.StopPrice} ");

                    if (currentAction == OrderAction.Buy)
					{
						// Move stop gain
						if (targetOrder.LimitPrice > updatedPrice && targetOrder.LimitPrice - updatedPrice <= 5)
						{
                            /*
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
							*/							

                            SetProfitTarget(SignalEntry1, CalculationMode.Price, targetOrder.LimitPrice + 5, false);
                            SetStopLoss(SignalEntry1, CalculationMode.Price, Math.Max(stopOrder.StopPrice, updatedPrice - 7), false);

                            Print($"Current GAIN: {targetOrder.LimitPrice}, updatedPrice: {updatedPrice}. Move GAIN to {targetOrder.LimitPrice + 5} - BUY");
						}
					}
					else if (currentAction == OrderAction.Sell)
					{
                        /*
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
						}
						*/

                        //ChangeOrder(targetOrder, 0, targetOrder.LimitPrice - 5, 0);						
                        //ChangeOrder(stopOrder, 0, 0, Math.Min(stopOrder.StopPrice, updatedPrice + 7));

                        SetProfitTarget(SignalEntry1, CalculationMode.Price, targetOrder.LimitPrice - 5, false);
                        SetStopLoss(SignalEntry1, CalculationMode.Price, Math.Min(stopOrder.StopPrice, updatedPrice + 7), false);

                        Print($"Move GAIN to {targetOrder.LimitPrice - 5} - SELL");						
					}												
				}
				// End of countTarget == 1 &&  countStop == 1
				
				/*
				* Keep it blank intentionally
				*/
			}
			catch(Exception e)
			{
				Print(e.Message);
			}
		}		
		/*
		* End of the class. Keep it blank intentionally.
		*/
	}
}
