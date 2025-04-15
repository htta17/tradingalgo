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
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    /**
     * Based class for ATM orders
     */
    public abstract class BarClosedATMBase<T1> : BarClosedBaseClass<T1, AtmStrategy>
    {
        public BarClosedATMBase(string name) : base(name) { }

        public BarClosedATMBase() : this("BASED ATM")
        {

        }

        #region Constants         
        protected const string OrderEntryName = "Entry";
        protected const string OrderStopName = "Stop";
        protected const string OrderTargetName = "Target";
        #endregion

        #region Configurations
        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Default ATM Strategy", Description = "Default ATM Strategy", Order = 1,
            GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string FullSizeATMName { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy",
            Description = "Strategy sử dụng khi loss/gain more than a half",
            Order = 2, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfSizefATMName { get; set; }        
        #endregion

        #region Properties
        protected AtmStrategy FullSizeAtmStrategy { get; set; }

        protected AtmStrategy HalfSizeAtmStrategy { get; set; }

        protected TradingStatus tradingStatus { get; set; } = TradingStatus.Idle;

        protected string AtmStrategyId = string.Empty;

        protected string OrderId = string.Empty;

        protected override TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        protected string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyATMBase.txt");

        DateTime enterOrderTimed = DateTime.MinValue;
        private DateTime executionTime = DateTime.MinValue;

        protected double StopLossPrice = -1;
        protected double TargetPrice_Full = -1;
        protected double TargetPrice_Half = -1;
        protected string CurrentChosenStrategy = "";
        #endregion

        protected override void CloseExistingOrders()
        {
            LocalPrint($"[CloseExistingOrders]");
            if (!string.IsNullOrEmpty(AtmStrategyId))
            {
                AtmStrategyClose(AtmStrategyId);
            }
            tradingStatus = TradingStatus.Idle;
        }

        protected override Order GetPendingOrder()
        {
            var order = Account.Orders.FirstOrDefault(c => c.Name.Contains(OrderEntryName) && (c.OrderState == OrderState.Working || c.OrderState == OrderState.Accepted));

            return order;
        }

        protected override void TransitionOrdersToLive()
        {
            if (TradingStatus == TradingStatus.OrderExists)
            {
                LocalPrint($"Transition to live, close all existing orders");

                CloseExistingOrders();
            }
            else if (TradingStatus == TradingStatus.PendingFill)
            {
                LocalPrint($"Transition to live, convert all pending fill orders to realtime.");
                var activeOrders = Account.Orders.Where(c => c.OrderState == OrderState.Working || c.OrderState == OrderState.Accepted).ToList();
                var len = activeOrders.Count;
                for (var i = 0; i < len; i++)
                {
                    var order = activeOrders[i];
                    var newOrder = GetRealtimeOrder(order);

                    CancelOrder(newOrder);
                }
            }
        }

        protected override void UpdatePendingOrderPure(double newPrice, double stopLossPrice, double target, double targetHalf)
        {
            if (Math.Abs(FilledPrice - newPrice) > 0.5)
            {
                FilledPrice = newPrice;
                StopLossPrice = stopLossPrice;
                TargetPrice_Full = target;
                TargetPrice_Half = targetHalf;

                try
                {
                    LocalPrint($"Trying to modify pending order, new Price: {newPrice:N2}, new stop loss: {stopLossPrice:N2}, new target: {target:N2}");

                    AtmStrategyChangeEntryOrder(newPrice, stopLossPrice, OrderId);
                }
                catch (Exception ex)
                {
                    LocalPrint($"[UpdatePendingOrder] - ERROR: {ex.Message}");
                }
            }
        }

        protected abstract void BasicActionForTrading(TimeFrameToTrade timeFrameToTrade);


        /// <summary>
        /// Sử dụng trong [OnStateChange] (State == State.Configure)
        /// </summary>        
        protected abstract void AddCustomDataSeries();

        /// <summary>
        /// Sử dụng trong [OnStateChange] (State == State.DataLoaded)
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        protected abstract void AddCustomIndicators();

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "General ATM based class";
            Description = "";

            tradingStatus = TradingStatus.Idle;

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";

            SetBreakEvenManually = false;
        }

        protected override void OnStateChange_Configure()
        {
            base.OnStateChange_Configure();

            ClearOutputWindow();

            AddCustomDataSeries();

            FullSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(FullSizeATMName, Print).AtmStrategy;

            HalfSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(HalfSizefATMName, Print).AtmStrategy;

            Print($"FullSizeAtmStrategy: [{FullSizeAtmStrategy.DisplayName}], HalfSizeAtmStrategy: [{HalfSizeAtmStrategy.DisplayName}]");
        }

        protected override void OnStateChange_Realtime()
        {
            base.OnStateChange_Realtime();

            // Load thông tin liên quan đến Status, OrderId và AtmStrategyId
            if (File.Exists(FileName))
            {
                try
                {
                    var text = File.ReadAllText(FileName);

                    var arr = text.Split(',');

                    if (arr.Length == 1)
                    {
                        AtmStrategyId = arr[0];
                    }
                    else if (arr.Length == 2)
                    {
                        AtmStrategyId = arr[0];
                        OrderId = arr[1];

                        tradingStatus = CheckCurrentStatusBasedOnOrders();
                        Print($"Initial status - {tradingStatus}");
                    }
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }
            }
        }

        protected void SaveAtmStrategyIdToFile(string strategyId, string orderId)
        {
            try
            {
                File.WriteAllText(FileName, $"{strategyId},{orderId}");

                LocalPrint($"Saved strategyId [{strategyId}] and orderId [{orderId}] to file");
            }
            catch (Exception e)
            {
                LocalPrint(e.Message);
            }
        }

        protected TradingStatus CheckCurrentStatusBasedOnOrders()
        {
            var activeOrders = Account.Orders
                                .Where(c => c.OrderState == OrderState.Accepted || c.OrderState == OrderState.Working)
                                .Select(c => new { c.OrderState, c.Name, c.OrderType })
                                .ToList();

            if (activeOrders.Count == 0)
            {
                return TradingStatus.Idle;
            }
            else if (activeOrders.Count == 1 && activeOrders[0].Name == OrderEntryName)
            {
                return TradingStatus.PendingFill;
            }
            else
            {
                return TradingStatus.OrderExists;
            }
        }

        protected override void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string atmStragtegyName, int quantity, bool isBuying, bool isSelling)
        {
            // Vào lệnh theo ATM 
            AtmStrategyId = GetAtmStrategyUniqueId();
            OrderId = GetAtmStrategyUniqueId();

            // Save to file, in case we need to pull [atmStrategyId] again
            SaveAtmStrategyIdToFile(AtmStrategyId, OrderId);

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            FilledPrice = priceToSet;

            // Enter a BUY/SELL order current price
            AtmStrategyCreate(
                action,
                OrderType.Limit,
                priceToSet,
                0,
                TimeInForce.Day,
                OrderId,
                atmStragtegyName,
                AtmStrategyId,
                (atmCallbackErrorCode, atmCallBackId) =>
                {
                    if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == AtmStrategyId)
                    {
                        tradingStatus = TradingStatus.PendingFill;
                    }
                    else if (atmCallbackErrorCode != ErrorCode.NoError)
                    {
                        LocalPrint($"[AtmStrategyCreate] ERROR : " + atmCallbackErrorCode);
                    }
                });
        }

        protected override void EnterOrder(T1 tradeAction)
        {
            if (State != State.Realtime || DateTime.Now.Subtract(enterOrderTimed).TotalSeconds < 5)
            {
                return;
            }
            enterOrderTimed = DateTime.Now;

            // Set global values
            CurrentTradeAction = tradeAction;

            EnteredBarIndex_5m = CurrentBarIndex_5m;

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            // Get stop loss and target ID based on strategy 
            var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(tradeAction);

            double priceToSet = GetSetPrice(tradeAction, atmStrategy);

            double stopLoss = GetStopLossPrice(tradeAction, priceToSet, atmStrategy);

            var targetHalf = GetTargetPrice_Half(tradeAction, priceToSet, atmStrategy);

            var targetFull = GetTargetPrice_Full(tradeAction, priceToSet, atmStrategy);

            FilledPrice = priceToSet;

            StopLossPrice = stopLoss;

            TargetPrice_Half = targetHalf;

            TargetPrice_Full = targetFull;

            CurrentChosenStrategy = atmStrategyName;

            LocalPrint($@"Enter {action}. Price to set: {priceToSet:N2}, StopLossPrice: {StopLossPrice:N2}, Target 1: {TargetPrice_Half:N2}, Target Full: {TargetPrice_Full:N2}");

            try
            {
                EnterOrderPure(priceToSet, 0, 0, atmStrategyName, 0, IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        protected override void CancelAllPendingOrder()
        {
            AtmStrategyCancelEntryOrder(OrderId);

            tradingStatus = TradingStatus.Idle;
        }

        /// <summary>
        /// Price is already be sure that > 100
        /// </summary>
        /// <param name="updatedPrice"></param>
        protected virtual void OnMarketData_PendingFill(double updatedPrice)
        { 
        
        }

        protected virtual void OnMarketData_OrderExists(double updatedPrice)
        {

        }

        protected virtual void OnMarketData_WatingForConfirmation(double updatedPrice)
        {

        }

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100)
            {
                return;
            }

            if (DateTime.Now.Subtract(executionTime).TotalSeconds < 1)
            {
                return;
            }

            executionTime = DateTime.Now;

            if (TradingStatus == TradingStatus.OrderExists)
            {
                var buyPriceIsOutOfRange = IsBuying && (updatedPrice < StopLossPrice || updatedPrice > TargetPrice_Full);
                var sellPriceIsOutOfRange = IsSelling && (updatedPrice > StopLossPrice || updatedPrice < TargetPrice_Full);

                // Khi giá đã ở ngoài range (stoploss, target)
                if (buyPriceIsOutOfRange || sellPriceIsOutOfRange)
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: OrderExists, new TradingStatus: {TradingStatus}. TargetPrice: {TargetPrice_Full:N2}, " +
                        $"updatedPrice:{updatedPrice:N2}, StopLossPrice: {StopLossPrice:N2}, " +
                        $"buyPriceIsOutOfRange: {buyPriceIsOutOfRange}, :sellPriceIsOutOfRange: {sellPriceIsOutOfRange}. ");

                    OnMarketData_OrderExists(updatedPrice);
                }
                else
                {
                    var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains(OrderStopName)).ToList();
                    var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains(OrderTargetName)).ToList();

                    var countStopOrder = stopOrders.Count;
                    var countTargetOrder = targetOrders.Count;

                    if (countStopOrder == 0 || countTargetOrder == 0)
                    {
                        tradingStatus = TradingStatus.Idle;
                        return;
                    }
                    else if (countStopOrder == 1 && countTargetOrder == 1)
                    {
                        var targetOrder = targetOrders.LastOrDefault();
                        var stopLossOrder = stopOrders.LastOrDefault();

                        if (targetOrder != null)
                        {
                            TargetPrice_Full = targetOrder.LimitPrice;
                            MoveTargetOrder(targetOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
                        }

                        if (stopLossOrder != null)
                        {
                            StopLossPrice = stopLossOrder.StopPrice;
                            MoveStopOrder(stopLossOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
                        }
                    }
                }
            }
            else if (TradingStatus == TradingStatus.PendingFill)
            {
                if ((IsBuying && updatedPrice < FilledPrice) || (IsSelling && updatedPrice > FilledPrice))
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: PendingFill, new TradingStatus: {TradingStatus}");
                }

                OnMarketData_PendingFill(updatedPrice);
            }
            else if (TradingStatus == TradingStatus.WatingForCondition)
            { 
            }
        }

        protected override bool IsFullPriceOrder(Order order)
        {
            // Don't need to implement
            throw new NotImplementedException();
        }

        protected override bool IsHalfPriceOrder(Order order)
        {
            // Don't need to implement
            throw new NotImplementedException();
        }

        protected override void MoveTargetOrStopOrder(double newPrice, Cbi.Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
        {
            try
            {
                var text = isGainStop ? "TARGET" : "LOSS";
                LocalPrint($"Dịch chuyển order [{order.Name}], id: {order.Id} ({text}), " +
                    $"{order.Quantity} contract(s) từ [{(isGainStop ? order.LimitPrice : order.StopPrice)}] " +
                    $"đến [{newPrice}] - {buyOrSell}");

                AtmStrategyChangeStopTarget(
                        isGainStop ? newPrice : 0,
                        isGainStop ? 0 : newPrice,
                        order.Name,
                        AtmStrategyId);

                if (isGainStop)
                {
                    TargetPrice_Full = newPrice;
                }
                else
                {
                    StopLossPrice = newPrice;
                }
            }
            catch (Exception ex)
            {
                LocalPrint($"[MoveTargetOrStopOrder] - ERROR: {ex.Message}");
            }
        }

        protected virtual (AtmStrategy, string) GetAtmStrategyByPnL(T1 tradeAction)
        {
            var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachHalf = 
                (todaysPnL <= (-MaximumDailyLoss / 2)) || (todaysPnL >= (DailyTargetProfit / 2));

            return reachHalf ? (HalfSizeAtmStrategy, HalfSizefATMName) : (FullSizeAtmStrategy, FullSizeATMName);
        }

        protected override double GetStopLossPrice(T1 tradeAction, double setPrice, AtmStrategy atmStrategy)
        {
            var stopLossTick = atmStrategy.Brackets[0].StopLoss;

            return IsBuying ?
                setPrice - stopLossTick * TickSize :
                setPrice + stopLossTick * TickSize;
        }

        protected override double GetTargetPrice_Half(T1 tradeAction, double setPrice, AtmStrategy atmStrategy)
        {
            var targetTick_Half = IsBuying ? atmStrategy.Brackets.Min(c => c.Target) : atmStrategy.Brackets.Max(c => c.Target);

            return IsBuying ?
                setPrice + targetTick_Half * TickSize :
                setPrice - targetTick_Half * TickSize;
        }

        protected override double GetTargetPrice_Full(T1 tradeAction, double setPrice, AtmStrategy atmStrategy)
        {
            var targetTick_Full = IsBuying ? atmStrategy.Brackets.Max(c => c.Target) : atmStrategy.Brackets.Min(c => c.Target);

            return IsBuying ?
                setPrice + targetTick_Full * TickSize :
                setPrice - targetTick_Full * TickSize;
        }

    }
}