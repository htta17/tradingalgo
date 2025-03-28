﻿#define TEST_ROOSTER

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
	public class GeneralBackTest : BarClosedBaseClass<GeneralTradeAction, GeneralTradeAction>
    {
        public GeneralBackTest() 
        {
#if TEST_ROOSTER
            HalfPriceSignals = new HashSet<string> { StrategiesUtilities.SignalEntry_GeneralHalf };

            EntrySignals = new HashSet<string>
            {
                StrategiesUtilities.SignalEntry_GeneralHalf,
                StrategiesUtilities.SignalEntry_GeneralFull
            };
#endif
        }
        protected override bool IsSelling => CurrentTradeAction == GeneralTradeAction.Sell;       

        protected override bool IsBuying => CurrentTradeAction == GeneralTradeAction.Buy;

        private EMA EMA46_5m { get; set; }
        private EMA EMA51_5m { get; set; }

        protected DateTime TouchEMA4651Time { get; set; } = DateTime.MinValue;



        protected double currentPrice = -1;

        protected double middleEma4651_5m = -1;
        protected double ema46_5m = -1;
        protected double ema51_5m = -1;

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;
        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            MaximumDailyLoss = 260;
            DailyTargetProfit = 500;

            StopLossInTicks = 120;
            Target1InTicks = 120;
            Target2InTicks = 160;
        }

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"For general back test.";
				Name										= "GeneralBackTest";
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
                // Add data for trading
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                CurrentTradeAction = GeneralTradeAction.NoTrade;
            }
            else if (State == State.DataLoaded)
            {
                EMA46_5m = EMA(46);
                EMA46_5m.Plots[0].Brush = Brushes.DarkOrange;

                EMA51_5m = EMA(51);
                EMA51_5m.Plots[0].Brush = Brushes.DeepSkyBlue;
                EMA51_5m.Plots[0].DashStyleHelper = DashStyleHelper.Dash;

                AddChartIndicator(EMA46_5m);
                AddChartIndicator(EMA51_5m);
            }
        }

		protected override void OnBarUpdate()
		{
            StrategiesUtilities.CalculatePnL(this, Account, Print);

            LocalPrint(1);
            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }
            LocalPrint(2);

            base.OnBarUpdate();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                currentPrice = Close[0];

                if (TradingStatus == TradingStatus.Idle)
                {                    
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if ((shouldTrade == GeneralTradeAction.Buy)
                        || (shouldTrade == GeneralTradeAction.Sell))
                    {
                        EnterOrder(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    // Cancel order
                    var shouldCancelOrder = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
                    if (shouldCancelOrder)
                    {
                        CancelAllPendingOrder();
                        return;
                    }

                    var shouldChangeVal = ShouldTrade();

                    // Nếu có vùng giá mới thì cập nhật
                    if (shouldChangeVal == GeneralTradeAction.NoTrade)
                    {
                        return;
                    }

                    if (shouldChangeVal == CurrentTradeAction)
                    {
                        var clonedList = ActiveOrders.Values.ToList();
                        var len = clonedList.Count;

                        var newPrice = GetSetPrice(shouldChangeVal, shouldChangeVal);

                        var stopLossPrice = GetStopLossPrice(shouldChangeVal, newPrice, shouldChangeVal);

                        var targetPrice_Half = GetTargetPrice_Half(shouldChangeVal, newPrice, shouldChangeVal);

                        var targetPrice_Full = GetTargetPrice_Full(shouldChangeVal, newPrice, shouldChangeVal);

                        for (var i = 0; i < len; i++)
                        {
                            var order = clonedList[i];
                            try
                            {
                                LocalPrint($"Trying to modify waiting order [{order.Name}], " +
                                    $"current Price: {order.LimitPrice}, current stop: {order.StopPrice}, " +
                                    $"new Price: {newPrice:N2}, new stop loss: {stopLossPrice}");

                                ChangeOrder(order, order.Quantity, newPrice, order.StopPrice);

                                SetStopLoss(order.Name, CalculationMode.Price, stopLossPrice, false);

                                if (IsHalfPriceOrder(order))
                                {
                                    SetProfitTarget(order.Name, CalculationMode.Price, targetPrice_Half, false);
                                }
                                else if (IsFullPriceOrder(order))
                                {
                                    SetProfitTarget(order.Name, CalculationMode.Price, targetPrice_Full, false);
                                }

                                FilledPrice = newPrice;
                            }
                            catch (Exception ex)
                            {
                                LocalPrint($"[UpdatePendingOrder] - ERROR: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // Hủy lệnh cũ và order lệnh mới 
                        CancelAllPendingOrder();

                        EnterOrder(shouldChangeVal);
                    }
                }
                else if (TradingStatus == TradingStatus.OrderExists)
                {
                    // Cập nhật lại target 2 
                }
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                currentPrice = Close[0];

                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                closePrice_5m = Close[0];
                openPrice_5m = Open[0];

                ema46_5m = EMA46_5m.Value[0];
                ema46_5m = EMA46_5m.Value[0];
                middleEma4651_5m = (EMA46_5m.Value[0] + EMA51_5m.Value[0]) / 2.0;

                if ((lowPrice_5m < ema46_5m && highPrice_5m > ema46_5m) || (lowPrice_5m < ema51_5m && highPrice_5m > ema51_5m))
                {
                    TouchEMA4651Time = Time[0];
                    LocalPrint($"Touch EMA46/51 at {TouchEMA4651Time}");
                }

                
            }
        }

        protected override bool IsHalfPriceOrder(Order order)
        {
            return order.Name == StrategiesUtilities.SignalEntry_FVGHalf;
        }

        protected override bool IsFullPriceOrder(Order order)
        {
            return order.Name == StrategiesUtilities.SignalEntry_FVGFull;
        }

        protected override double GetSetPrice(GeneralTradeAction tradeAction, GeneralTradeAction additionalInfo)
        {
            return StrategiesUtilities.RoundPrice(middleEma4651_5m);
        }

        protected override double GetTargetPrice_Half(GeneralTradeAction tradeAction, double setPrice, GeneralTradeAction additionalInfo)
        {
            return tradeAction == GeneralTradeAction.Buy ?
                    setPrice + (Target1InTicks * TickSize) : setPrice - (Target1InTicks * TickSize);
        }

        protected override double GetTargetPrice_Full(GeneralTradeAction tradeAction, double setPrice, GeneralTradeAction additionalInfo)
        {
            return tradeAction == GeneralTradeAction.Buy ?
                    setPrice + (Target2InTicks * TickSize) : setPrice - (Target2InTicks * TickSize);
        }

        protected override double GetStopLossPrice(GeneralTradeAction tradeAction, double setPrice, GeneralTradeAction additionalInfo)
        {
            return tradeAction == GeneralTradeAction.Buy ?
                    setPrice - (StopLossInTicks * TickSize) : setPrice + (StopLossInTicks * TickSize);
        }

        protected override GeneralTradeAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Trước 9:10am hoặc sau 2:00pm thì không nên trade 
            if (time < 091000 && time < 140000)
            {
                LocalPrint($"Rooster chỉ sử dụng từ 9:10a-2:00pm --> No Trade.");
                return GeneralTradeAction.NoTrade;
            }

            var totalMinutes = Time[0].Subtract(TouchEMA4651Time).TotalMinutes;
            var distanceToEMA = Math.Abs(middleEma4651_5m - currentPrice);
            var tradeReversal = totalMinutes > 60 && distanceToEMA < 20;

            var logText = @$"
                    Last touch EMA46/51: {TouchEMA4651Time:HH:mm}, 
                    Total minutes until now:  {totalMinutes}, 
                    Distance to middle of EMA46/51: {distanceToEMA:N2}.
                    --> Trade REVERSAL (totalMinutes > 60 && distanceToEMA < 20): {tradeReversal}";

            LocalPrint(logText);

            if (tradeReversal) // Nếu đã chạm EMA46/51 lâu rồi 
            {
                if (closePrice_5m > middleEma4651_5m && openPrice_5m > middleEma4651_5m)
                {
                    LocalPrint($"Đủ điều kiện cho BUY REVERSAL: {logText}");
                    return GeneralTradeAction.Buy;
                }
                else if (closePrice_5m < middleEma4651_5m && openPrice_5m < middleEma4651_5m)
                {
                    LocalPrint($"Đủ điều kiện cho SELL REVERSAL: {logText}");
                    return GeneralTradeAction.Sell;
                }
            }

            return GeneralTradeAction.NoTrade;
        }

        protected override void EnterOrder(GeneralTradeAction action)
        {
            // Set global values
            CurrentTradeAction = action;            

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var orderAction = action == GeneralTradeAction.Buy ? OrderAction.Buy : OrderAction.Sell;

            try
            {
                double priceToSet = GetSetPrice(action, action);
                FilledPrice = priceToSet;

                var stopLossPrice = GetStopLossPrice(action, priceToSet, action);
                var targetHalf = GetTargetPrice_Half(action, priceToSet, action);
                var targetFull = GetTargetPrice_Full(action, priceToSet, action);                

                EnterOrderPureUsingPrice(priceToSet, targetHalf, stopLossPrice,
                    StrategiesUtilities.SignalEntry_FVGHalf, 2,
                    IsBuying, IsSelling);

                EnterOrderPureUsingPrice(priceToSet, targetFull, stopLossPrice,
                    StrategiesUtilities.SignalEntry_FVGFull, 2,
                    IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        protected override void UpdatePendingOrderPure(double newPrice, double stopLossPrice, double targetFull, double? targetHalf = null)
        {
            throw new NotImplementedException();
        }
    }
}
