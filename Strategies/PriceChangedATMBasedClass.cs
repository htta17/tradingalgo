#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Windows.Media;
using System.Collections.Generic;
using NinjaTrader.Custom.Strategies;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using NinjaTrader.Gui;
using System.Windows;
using System.IO;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T1">Should Trade answer</typeparam>
    public abstract class PriceChangedATMBasedClass<T1, T2> : Strategy
    {
        private string LogPrefix { get; set; }        
        protected const string OrderEntryName = "Entry";
        protected const string OrderStopName = "Stop";
        protected const string OrderTargetName = "Target";

        protected List<int> NewsTimes = new List<int>();

        /// <summary>
        /// If loss is more than [MaximumDayLoss], stop trading for that day
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Maximum Day Loss ($)",
            Order = 5,
            GroupName = StrategiesUtilities.Configuration_DailyPnL_Name)]
        public int MaximumDailyLoss { get; set; }

        /// <summary>
        /// If gain is more than [StopWhenGain], stop trading for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop Trading if daily Profit is ($)",
            Order = 6,
            GroupName = StrategiesUtilities.Configuration_DailyPnL_Name)]
        public int DailyTargetProfit { get; set; } = 500;

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

        /// <summary>
        /// Risky live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Risky Strategy",
            Description = "Strategy sử dụng khi lệnh là risky",
            Order = 2, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string RiskyATMName { get; set; }

        protected AtmStrategy FullSizeAtmStrategy { get; set; }

        protected AtmStrategy HalfSizeAtmStrategy { get; set; }

        protected AtmStrategy RiskyAtmStrategy { get; set; }

        protected TradingStatus tradingStatus { get; set; } = TradingStatus.Idle;

        protected bool StartMovingStoploss { get; set; }

        protected TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        protected T1 CurrentTradeAction { get; set; }

        protected string AtmStrategyId = string.Empty;

        protected string OrderId = string.Empty;

        protected abstract bool IsBuying { get; }

        protected abstract bool IsSelling { get; }

        protected double FilledPrice = -1;
        protected double StopLossPrice = -1;
        protected double TargetPrice_Full = -1;
        protected double TargetPrice_Half = -1;

        protected double PointToMoveTarget { get; set; }

        protected double PointToMoveLoss { get; set; }

        public PriceChangedATMBasedClass() : this("BASE")
        { 
        }

        protected virtual Order GetPendingOrder()
        {
            var order = Account.Orders.FirstOrDefault(c => c.Name.Contains(OrderEntryName) && (c.OrderState == OrderState.Working || c.OrderState == OrderState.Accepted));

            return order;
        }

        protected virtual bool ShouldCancelPendingOrdersByTimeCondition(DateTime filledOrderTime)
        {
            if ((Time[0] - filledOrderTime).TotalMinutes > 60)
            {
                //Account.CancelAllOrders(Instrument);
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh do đợi quá lâu, Time[0]: {Time[0]}, filledTime: {filledOrderTime}");
                return true;
            }

            // Cancel lệnh hết giờ trade
            if (ToTime(Time[0]) >= 150000 && ToTime(filledOrderTime) < 150000)
            {
                //Account.CancelAllOrders(Instrument);
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh hết giờ trade");
                return true;
            }

            return false;
        }

        protected DateTime FilledTime = DateTime.Now;

        protected virtual void UpdatePendingOrder()
        {
            if (TradingStatus != TradingStatus.PendingFill)
            {
                return;
            }
            
            var firstOrder = GetPendingOrder();

            if (firstOrder == null)
            {
                return;
            }

            // Cancel lệnh do đợi quá lâu            

            var cancelOrderDueByTime = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
            if (cancelOrderDueByTime)
            {
                return;
            }

            /*
            var checkShouldTradeAgain = ShouldTrade();

            if (checkShouldTradeAgain == TradeAction.NoTrade)
            {
                LocalPrint($"Check lại các điều kiện với [ShouldTrade], new answer: [{checkShouldTradeAgain}] --> Cancel lệnh do không thỏa mãn các điều kiện trade");
                CancelAllPendingOrder();
                return;
            }
            else
            {
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(checkShouldTradeAgain);

                var newPrice = GetSetPrice(checkShouldTradeAgain, atmStrategy);

                var stopLossPrice = GetStopLossPrice(checkShouldTradeAgain, newPrice, atmStrategy);

                var targetPrice_Half = GetTargetPrice_Half(checkShouldTradeAgain, newPrice, atmStrategy);

                var targetPrice_Full = GetTargetPrice_Full(checkShouldTradeAgain, newPrice, atmStrategy);

                // Số lượng contracts hiện tại

                // Nếu ngược trend hoặc backtest thì vào cancel lệnh cũ và vào lệnh mới
                if (State == State.Historical || (CurrentTradeAction != checkShouldTradeAgain) || (CurrentChosenStrategy != atmStrategyName))
                {
                    #region Cancel current order and enter new one
                    CancelAllPendingOrder();

                    EnterOrder(checkShouldTradeAgain);
                    #endregion
                }
                // Ngược lại thì update điểm vào lệnh
                else if (State == State.Realtime)
                {
                    #region Begin of move pending order
                    UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);
                    #endregion
                }           
            }
             */



        }

        public PriceChangedATMBasedClass(string logPrefix)
        {
            LogPrefix = logPrefix;
        }

        protected virtual void SetDefaultProperties()
        {
            FullSizeATMName = "Roses_Default_4cts";
            HalfSizefATMName = "Roses_Default_4cts";
            RiskyATMName = "Roses_Default_4cts";

            PointToMoveTarget = 3;
            PointToMoveLoss = 10;
        }

        protected bool IsTradingHour()
        {
            var time = ToTime(Time[0]);

            var newTime = NewsTimes.FirstOrDefault(c => StrategiesUtilities.NearNewsTime(time, c));

            if (newTime != 0)
            {
                LocalPrint($"News at {newTime} --> Not trading hour");
                return false;
            }

            return true;
        }

        protected abstract double GetSetPrice(T1 tradeAction, T2 additional);        
        protected abstract double GetTargetPrice_Half(T1 tradeAction, double setPrice, T2 additional);
        protected abstract double GetTargetPrice_Full(T1 tradeAction, double setPrice, T2 additional);
        protected abstract double GetStopLossPrice(T1 tradeAction, double setPrice, T2 additional);

        protected string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyATMBase.txt");

        protected virtual bool CheckingTradeCondition(ValidateType validateType = ValidateType.MaxDayGainLoss | ValidateType.TradingHour)
        {
            // Không đủ số lượng Bar
            if (CurrentBar < BarsRequiredToTrade)
            {
                return false;
            }

            // Không phải trading hour
            if ((validateType & ValidateType.TradingHour) == ValidateType.TradingHour && !IsTradingHour())
            {
                if (TradingStatus == TradingStatus.Idle)
                {
                    return false;
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    LocalPrint($"Gần giờ có news, cancel những lệnh chờ đang có");
                    CancelAllPendingOrder();
                    return false;
                }              
            }

            // Đủ target loss/gain trong ngày
            if ((validateType & ValidateType.MaxDayGainLoss) == ValidateType.MaxDayGainLoss &&
                StrategiesUtilities.ReachMaxDayLossOrDayTarget(this, Account, MaximumDailyLoss, DailyTargetProfit))
            {
                return false;
            }

            return true;
        }

        protected void DrawLine(string name, double value, Brush lineColor, Brush textColor, DashStyleHelper dashStyle = DashStyleHelper.Dot, int textPosition = -3, string labelText = "")
        {
            Draw.HorizontalLine(this, name, value, lineColor, dashStyle, 2);
            Draw.Text(this, $"{name}_label", true, string.IsNullOrEmpty(labelText) ? $"[{value:N2}]" : labelText,
                textPosition,
                value,
                5,
                textColor,
                new SimpleFont("Arial", 10),
                TextAlignment.Left,
                Brushes.Transparent,
                Brushes.Transparent, 0);
        }

        /// <summary>
        /// Move target order
        /// </summary>
        /// <param name="targetOrder"></param>
        /// <param name="updatedPrice"></param>
        /// <param name="filledPrice"></param>
        /// <param name="isBuying"></param>
        /// <param name="isSelling"></param>        
        protected virtual void MoveTargetOrder(Order targetOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            var targetOrderPrice = targetOrder.LimitPrice;

            // Dịch stop gain nếu giá quá gần target            
            if (isBuying && updatedPrice + PointToMoveTarget > targetOrderPrice)
            {
                LocalPrint($"[MoveTargetOrder] - Moving target BUY --> True ({updatedPrice:N2} + {PointToMoveTarget} > {targetOrderPrice:N2})");

                MoveTargetOrStopOrder(targetOrderPrice + PointToMoveTarget, targetOrder, true, "BUY", targetOrder.FromEntrySignal);

                StartMovingStoploss = true;
            }
            else if (isSelling && updatedPrice - PointToMoveTarget < targetOrderPrice)
            {
                LocalPrint($"[MoveTargetOrder] - Moving target SELL --> True ({updatedPrice:N2} - {PointToMoveTarget} < {targetOrderPrice:N2})");

                MoveTargetOrStopOrder(targetOrderPrice - PointToMoveTarget, targetOrder, true, "SELL", targetOrder.FromEntrySignal);

                StartMovingStoploss = true;
            }
        }

        protected virtual void MoveStopOrder(Order stopOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            double newPrice = -1;
            var allowMoving = false;
            var stopOrderPrice = stopOrder.StopPrice;

            if (isBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice > filledPrice && stopOrderPrice + PointToMoveLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveLoss;
                    allowMoving = true;
                }
                else if (updatedPrice - filledPrice >= 60 && stopOrderPrice - filledPrice < 40)
                {
                    newPrice = filledPrice + 40;
                    allowMoving = true;
                }
                else if (updatedPrice - filledPrice >= 30 && stopOrderPrice - filledPrice < 10)
                {
                    newPrice = filledPrice + 10;
                    allowMoving = true;
                }
                else
                {
                    #region Code cũ - Có thể sử dụng lại sau này
                    /*
                     * Old code cho trường hợp stop loss đã về ngang với giá vào lệnh (break even). 
                     * - Có 2x contracts, cắt x contract còn x contracts
                     * - Khi giá lên [Target_Half + 7] thì đưa stop loss lên Target_Half
                     */

                    /*
                    allowMoving = allowMoving || (filledPrice <= stopOrderPrice && stopOrderPrice < TargetPrice_Half && TargetPrice_Half + 7 < updatedPrice);

                    LocalPrint($"Điều kiện để chuyển stop lên target 1 - filledPrice: {filledPrice:N2} <= stopOrderPrice: {stopOrderPrice:N2} <= TargetPrice_Half {TargetPrice_Half:N2} --> Allow move: {allowMoving}");

                    // Giá lên 37 điểm thì di chuyển stop loss lên 30 điểm
                    if (allowMoving)
                    {
                        newPrice = TargetPrice_Half;                        
                    }
                    */
                    #endregion
                }
            }
            else if (isSelling)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice < filledPrice && stopOrderPrice - PointToMoveLoss > updatedPrice)
                {
                    newPrice = updatedPrice + PointToMoveLoss;
                    allowMoving = true;
                }
                else if (filledPrice - updatedPrice >= 60 && filledPrice - stopOrderPrice < 40)
                {
                    newPrice = filledPrice - 40;
                    allowMoving = true;
                }
                else if (filledPrice - updatedPrice >= 30 && filledPrice - stopOrderPrice < 10)
                {
                    newPrice = filledPrice - 10;
                    allowMoving = true;
                }
                else
                {
                    #region Code cũ - Có thể sử dụng lại sau này
                    /*
                     * Old code cho trường hợp stop loss đã về ngang với giá vào lệnh (break even). 
                     * - Có 2x contracts, cắt x contract còn x contracts
                     * - Khi giá lên [Target_Half + 7] thì đưa stop loss lên Target_Half
                     */

                    /*
                    allowMoving = allowMoving || (filledPrice >= stopOrderPrice && stopOrderPrice > TargetPrice_Half && TargetPrice_Half - 7 > updatedPrice);

                    LocalPrint($"Điều kiện để chuyển stop lên target 1  - filledPrice: {filledPrice:N2} >= stopOrderPrice: {stopOrderPrice:N2} > TargetPrice_Half {TargetPrice_Half:N2} --> Allow move: {allowMoving}");

                    if (allowMoving)
                    {
                        newPrice = TargetPrice_Half;
                    }
                    */
                    #endregion                    
                }
            }

            if (allowMoving)
            {
                LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{updatedPrice}]");

                MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
            }
        }


        protected virtual void MoveTargetOrStopOrder(double newPrice, Cbi.Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
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


        protected virtual void OnStateChange_Configure()
        { 
            
        }

        /// <summary>
        /// Hiện tại, [OnStateChange] đã có code: <br/>
        ///     • State == State.SetDefaults: Có thể set defaults thêm, sử dụng [SetDefaultProperties] <br/>
        ///     • State == State.Configure: Added data of 5m, 3m, and 1m. Add indicators: EMA(BarsArray[1], 46)[0] sẽ add indicator EMA64 cho khung 5 phút <br/>
        ///     (BarsArray[1] sẽ là info của nến khung 5 phút) <br/>
        ///     • State == State.DataLoaded: [AddIndicators]
        ///     
        /// </summary>
        protected sealed override void OnStateChange()
        {
            base.OnStateChange();
            if (State == State.SetDefaults)
            {
                Description = "Price Changed & ATM Based Class";
                Name = "Price Action Realtime";
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TraceOrders = false;
                BarsRequiredToTrade = 51;
                IsInstantiatedOnEachOptimizationIteration = true;

                SetDefaultProperties();
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);                
                AddDataSeries(BarsPeriodType.Minute, 1);                

                try
                {
                    FullSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(FullSizeATMName, Print).AtmStrategy;

                    HalfSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(HalfSizefATMName, Print).AtmStrategy;

                    RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyATMName, Print).AtmStrategy;
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }                
            }
            else if (State == State.DataLoaded)
            {
                AddIndicators();                
            }
            else if (State == State.Realtime)
            {
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
                            Print($"Initial status - {tradingStatus}, found AtmStrategyId: {AtmStrategyId}, OrderId: {OrderId}");
                        }
                    }
                    catch (Exception e)
                    {
                        Print(e.Message);
                    }
                }

                /*
                try
                {
                    // Nếu có lệnh đang chờ thì cancel các lệnh hiện có bắt đầu chuyển về dùng ATM
                    TransitionOrdersToLive();
                }
                catch (Exception e)
                {
                    LocalPrint("[OnStateChange] - State change to Realtime - ERROR: " + e.Message);
                }
                */
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


        protected virtual void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string atmStragtegyName, int quantity, bool isBuying, bool isSelling)
        {
            // Vào lệnh theo ATM 
            AtmStrategyId = GetAtmStrategyUniqueId();
            OrderId = GetAtmStrategyUniqueId();

            // Save to file, in case we need to pull [atmStrategyId] again
            SaveAtmStrategyIdToFile(AtmStrategyId, OrderId);

            var action = isBuying ? OrderAction.Buy : OrderAction.Sell;

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
                        // Set trading status to Pending Fill
                        tradingStatus = TradingStatus.PendingFill;
                    }
                    else if (atmCallbackErrorCode != ErrorCode.NoError)
                    {
                        LocalPrint($"[AtmStrategyCreate] ERROR : " + atmCallbackErrorCode);
                    }
                });
        }

        #region Helpers - Make sure not too many times OnBarUpdate is counted
        private DateTime lastExecutionTime_5m = DateTime.MinValue;        
        private DateTime lastExecutionTime_1m = DateTime.MinValue;

        int lastBar_5m = -1;        
        int lastBar_1m = -1;

        bool triggerLastBar_1m = false;        
        bool triggerLastBar_5m = false;

        private bool RunningTooFast(int barsPeriod)
        {
            bool isTooFast = false;            
            
            if (barsPeriod == 1)
            {
                if (State == State.Historical)
                {
                    OnBarUpdate_StateHistorical(barsPeriod);
                }
                else if (State == State.Realtime)
                {
                    isTooFast = DateTime.Now.Subtract(lastExecutionTime_1m).TotalSeconds < 1;

                    if (!isTooFast)
                    {
                        lastExecutionTime_1m = DateTime.Now;

                        if (Time[0].Subtract(DateTime.Now).TotalSeconds < 1 && !triggerLastBar_1m)
                        {
                            triggerLastBar_1m = true;

                            OnCurrentBarClosed(barsPeriod);
                        }
                        else
                        {
                            OnRegularTick(barsPeriod);
                        }
                    }
                }                                
            }           
            else if (barsPeriod == 5)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_5m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_5m = DateTime.Now;                    

                    if (Time[0].Subtract(DateTime.Now).TotalSeconds < 1 && !triggerLastBar_5m)
                    {
                        triggerLastBar_5m = true;

                        OnCurrentBarClosed(barsPeriod);
                    }
                    else
                    {
                        OnRegularTick(barsPeriod);
                    }
                }                
            }

            return isTooFast;
        }

        /// <summary>
        /// Do with regular tick, happens in realtime only
        /// </summary>
        /// <param name="barsPeriod"></param>
        protected virtual void OnRegularTick(int barsPeriod)
        {
            
        }

        protected int GetBarIndex(int barsPeriod)
        {
            return barsPeriod == 5 ? 1 : 2;
        }

        protected void LocalPrint(object val)
        {
            if (val.GetType() == typeof(string))
            {
                Print($"[{LogPrefix}]-{Time?[0]}-" + val);
            }
            else
            {
                Print(val);
            }
        }

        /// <summary>
        /// Excecute when it moved to new bar
        /// </summary>
        /// <param name="barsPeriod"></param>
        protected abstract void OnNewBarCreated(int barsPeriod);

        protected abstract T1 ShouldTrade();

        protected virtual void EnterOrderHistorical(T1 action)
        {

        }

        protected virtual (AtmStrategy, string) GetAtmStrategyByPnL(T1 tradeAction)
        {
            var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachHalf =
                (todaysPnL <= (-MaximumDailyLoss / 2)) || (todaysPnL >= (DailyTargetProfit / 2));

            return reachHalf ? (HalfSizeAtmStrategy, HalfSizefATMName) : (FullSizeAtmStrategy, FullSizeATMName);
        }

        protected abstract void EnterOrderRealtime(T1 action);

        protected void EnterOrder(T1 action)
        {
            if (State == State.Historical)
            {
                // Enter với stop loss và stop gain 
                EnterOrderHistorical(action);
            }
            else if (State == State.Realtime)
            {                
                EnterOrderRealtime(action);
            }
        }

        /// <summary>
        /// Add indicators
        /// </summary>
        protected abstract void AddIndicators();

        /// <summary>
        /// Excecute when current bar close (last tick of current bar)
        /// </summary>
        /// <param name="barsPeriod"></param>
        protected abstract void OnCurrentBarClosed(int barsPeriod);

        protected virtual void TransitionOrdersToLive()
        {
            if (TradingStatus == TradingStatus.OrderExists)
            {
                LocalPrint($"Transition to live, close all ActiveOrders");

                CloseExistingOrders();
            }
            else if (TradingStatus == TradingStatus.PendingFill)
            {
                var pendingOrders = Account.Orders.Where(c => c.OrderState == OrderState.Working && c.Name == OrderEntryName).ToList(); 
                for (var i = 0; i < pendingOrders.Count; i++)
                {                    
                    var newOrder = GetRealtimeOrder(pendingOrders[i]);
                    CancelOrder(newOrder);
                }
            }
        }

        protected virtual void CloseExistingOrders()
        {

        }

        protected virtual void CancelAllPendingOrder()
        {
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
        #endregion

        protected virtual void OnBarUpdate_StateHistorical(int barsPeriod)
        { 
            
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == 0) // BarsInProgress == 0 dùng cho view hiện tại, ko làm gì
            {
                return; 
            }

            if (RunningTooFast(BarsPeriod.Value))
            {
                return;
            }    

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                if (State == State.Realtime)
                {
                    if (lastBar_1m != CurrentBar)
                    {
                        triggerLastBar_1m = false;
                        OnNewBarCreated(BarsPeriod.Value);
                        lastBar_1m = CurrentBar;
                    }
                }
                else if (State == State.Historical)
                {
                    OnBarUpdate_StateHistorical(BarsPeriod.Value);
                }
            }           
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) //5 minute
            {
                if (State == State.Realtime)
                {
                    if (lastBar_5m != CurrentBar)
                    {
                        triggerLastBar_5m = false;
                        OnNewBarCreated(BarsPeriod.Value);
                        lastBar_5m = CurrentBar;
                    }
                }
                else if (State == State.Historical)
                {
                    OnBarUpdate_StateHistorical(BarsPeriod.Value);
                }
            }
        }
    }
}
