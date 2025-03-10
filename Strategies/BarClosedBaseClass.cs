﻿using System.Web.Script.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Markup;
using System.Xml.Linq;

namespace NinjaTrader.Custom.Strategies
{
    /**
     * Based Class cho các Strategies sử dụng tính toán khi đóng cây nến [OnBarClose]. Lưu ý các điểm sau: 
     * 1. Luôn luôn vào 2 order, 1 half size và 1 full size. Dịch stop loss khi break even hiện tại đang dựa khi số lượng order là 1
     */
    public abstract class BarClosedBaseClass<T1> : NinjaTrader.NinjaScript.Strategies.Strategy
    {
        private string LogPrefix { get; set; }        
        public BarClosedBaseClass(string logPrefix)
        {
            LogPrefix = logPrefix;
        }

        public BarClosedBaseClass() : this("[BASED]")
        {
            
        }

        protected int CurrentBarIndex_5m = 0;
        protected int EnteredBarIndex_5m = 0;

        protected int CurrentOrderCount { get; set; }

        protected T1 CurrentTradeAction { get; set; }

        /// <summary>
        /// Biến này dùng để di chuyển stop loss khi giá BẮT ĐẦU gần chạm đến target2 (để room cho chạy).
        /// </summary>
        protected bool StartMovingStoploss = false;

        /// <summary>
        /// If loss is more than [MaximumDayLoss], stop trading for that day
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Cho phép ghi log",
            Order = 1,
            GroupName = StrategiesUtilities.Configuration_General_Name)]
        public bool AllowWriteLog { get; set; }

        /// <summary>
        /// If = true: Set break even manually, otherwise, ATM will set breakeven.
        /// </summary>
        protected bool SetBreakEvenManually = true;

        #region Allow Trade Parameters

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
        #endregion

        /// <summary>
        /// If gain is more than [StopWhenGain], stop trading for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lượng contract: ",
            Order = 6,
            GroupName = StrategiesUtilities.Configuration_DailyPnL_Name)]
        public int NumberOfContract { get; set; }


        #region Stoploss/Profit

        /// <summary>
        /// Cho phép dịch chuyển stop loss và target
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Allow to move stop loss/profit target",
            Order = 14,
            GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]
        public bool AllowToMoveStopLossGain { get; set; } = true;

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop loss (Ticks):",
            Order = 15,
            GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]
        public int StopLossInTicks { get; set; } = 120; // 25 points for MNQ

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Target 1 Profit (Ticks):",
            Order = 16,
            GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]
        public int Target1InTicks { get; set; } = 40; // 10 points for MNQ

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Target 2 Profit (Ticks):",
            Order = 17,
            GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]
        public int Target2InTicks { get; set; } = 120; // 25 points for MNQ        

        /// <summary>
        /// Giá hiện tại cách target &lt; [PointToMoveTarget] thì di chuyển target.
        /// </summary>
        protected double PointToMoveTarget = 3;

        /// <summary>
        /// Giá hiện tại cách stop loss > [PointToMoveLoss] thì di chuyển stop loss.
        /// </summary>
        protected double PointToMoveLoss = 7;        
        #endregion        

        /// <summary>
        /// Đọc thông tin 
        /// </summary>
        /// <returns></returns>
        private string ReadNewsInfoFromFile()
        {
            var filePath = @"\\JOYFUL\TradingFolder\WeekNewsTime.txt";

            try
            {
                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();

                    var data = serializer.Deserialize< NewsTimeReader>(jsonContent);

                    var today = DateTime.Today.DayOfWeek;

                    var newsTime = string.Empty;

                    switch (today)
                    { 
                        case DayOfWeek.Sunday:
                            return data.Sunday;
                        case DayOfWeek.Monday: 
                            return data.Monday;
                        case DayOfWeek.Tuesday:
                            return data.Tuesday;
                        case DayOfWeek.Wednesday:
                            return data.Wednesday;
                        case DayOfWeek.Thursday:
                            return data.Thursday;
                        case DayOfWeek.Friday:
                            return data.Friday;
                        // No Saturday, ok? 
                    }
                    
                    Print(jsonContent);
                }
            }
            catch (Exception ex)
            {
                Print(ex.ToString());
            }
            return string.Empty; 
        }

        protected virtual void UpdatePendingOrder()
        {

        }

        protected virtual void SetDefaultProperties()
        {
            MaximumDailyLoss = 260;
            DailyTargetProfit = 500;
            AllowToMoveStopLossGain = true;           

            StopLossInTicks = 120;
            Target1InTicks = 60;
            Target2InTicks = 120;

            AllowToMoveStopLossGain = true;            

            PointToMoveTarget = 3;
            PointToMoveLoss = 7;

            AllowWriteLog = true;

            NumberOfContract = 1; 

            //FiveMinutes_Trends = Trends.Unknown;

            CountOrder = 0;
            CountEntrySignal = 0;
        }

        protected abstract void UpdatePendingOrderPure(double newPrice, double stopLossPrice, double targetFull);

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

        protected virtual TradingStatus TradingStatus
        {
            get
            {
                if (CountOrder == 0)
                {
                    return TradingStatus.Idle;
                }
                else if (CountEntrySignal > 0)
                {
                    return TradingStatus.PendingFill;
                }

                return TradingStatus.OrderExists;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1 && TradingStatus == TradingStatus.OrderExists) //1 minute
            {
                // Close all current orders nếu sau 3:50pm
                var currentTime = ToTime(DateTime.Now);
                if (currentTime >= 155000 && currentTime < 160000)
                {
                    CloseExistingOrders();
                }
            }
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Based Class for all Strategies which is triggered to execute with [Calculate] is [OnBarClose].";
                // Let not set Name here, each inheritted class will set by itself
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 2;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = Cbi.TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.IgnoreAllErrors;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;

                SetDefaultProperties();
            }
            else if (State == State.Configure)
            {
                try
                {
                    ClearOutputWindow();

                    var newsFromFile = ReadNewsInfoFromFile();

                    if (newsFromFile != string.Empty)
                    {
                        newsFromFile = $"{StrategiesUtilities.DefaultNewsTime},{newsFromFile}";
                        Print($"[NewsTime]: {newsFromFile}");
                    }
                    else // Nếu ngày hôm nay không có gì thì chỉ lấy thời gian mở, đóng cửa. 
                    {
                        newsFromFile = StrategiesUtilities.DefaultNewsTime; 
                    }

                    NewsTimes = newsFromFile.Split(',').Select(c => int.Parse(c)).ToList();
                }
                catch (Exception e)
                {
                    Print($"[OnStateChange] - ERROR: " + e.Message);
                }
            }
            else if (State == State.Realtime)
            {
                try
                {
                    // Nếu có lệnh đang chờ thì cancel 
                    TransitionOrdersToLive();
                }
                catch (Exception e)
                {
                    LocalPrint("[OnStateChange] - ERROR" + e.Message);
                }
            }
        }

        protected virtual void CancelAllPendingOrder()
        {
            var clonedList = ActiveOrders.Values.ToList();
            var len = clonedList.Count;
            for (var i = 0; i < len; i++)
            {
                var order = clonedList[i];
                CancelOrder(order);
            }
        }

        protected virtual void UpdateEntryOrder(double setPrice, double stopLoss, double stopGain)
        {

        }

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
                /*
                else if (ChickenStatus == ChickenStatus.OrderExists) // Đang có lệnh
                {
                    var unrealizedProfit = Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);

                    // Đang lỗ lệnh này --> Tiếp tục keep và hi vọng tương lai tương sáng với news
                    if (unrealizedProfit < 0)
                    {
                        return; 
                    }

                    if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1)
                    {
                        var updatedPrice = Close[0];

                        // Nếu đang có lời thì dời toàn bộ stop loss lên break even 
                        var stopLossOrders = IsBuying
                            ? ActiveOrders.Values.Where(c => c.OrderType == OrderType.StopLimit && c.StopPrice < filledPrice && filledPrice < updatedPrice).ToList()
                            : ActiveOrders.Values.Where(c => c.OrderType == OrderType.StopLimit && c.StopPrice > filledPrice && filledPrice > updatedPrice).ToList();

                        foreach (var stopLossOrder in stopLossOrders)
                        {
                            MoveTargetOrStopOrder(filledPrice, stopLossOrder, false, IsBuying ? "BUY" : "SELL", stopLossOrder.FromEntrySignal); 
                        }
                    }
                }
                */
            }

            // Đủ target loss/gain trong ngày
            if ((validateType & ValidateType.MaxDayGainLoss) == ValidateType.MaxDayGainLoss && 
                StrategiesUtilities.ReachMaxDayLossOrDayTarget(this, Account, MaximumDailyLoss, DailyTargetProfit))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Realtime: Dùng order.Id làm key, không phải Realtime: Dùng Name làm key
        /// </summary>
        protected Dictionary<string, Order> ActiveOrders = new Dictionary<string, Order>();

        protected Dictionary<string, SimpleInfoOrder> SimpleActiveOrders = new Dictionary<string, SimpleInfoOrder>();

        private readonly object lockOjbject = new Object();

        /// <summary>
        /// Dùng để check xem có lệnh nào không
        /// </summary>
        protected int CountOrder { get; set; }

        /// <summary>      
        /// Dùng để check xem có lệnh chờ (PendingFill order) hay không.
        /// </summary>
        protected int CountEntrySignal { get; set; }
        protected override void OnOrderUpdate(Order order,
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
            var focusedOrderState = (orderState == OrderState.Filled || orderState == OrderState.Cancelled || orderState == OrderState.Working || orderState == OrderState.Accepted);

            if (!focusedOrderState)
            {
                return;
            }
            var key = StrategiesUtilities.GenerateKey(order);

            try
            {   
                if (orderState == OrderState.Filled || orderState == OrderState.Cancelled)
                {
                    ActiveOrders.Remove(key);
                    SimpleActiveOrders.Remove(key);

                    CountOrder--;

                    // Nếu đang có signal 
                    if (EntrySignals.Contains(key))
                    {
                        CountEntrySignal--; 
                    }
                }
                else if (orderState == OrderState.Working || orderState == OrderState.Accepted)
                {
                    // Add or update 
                    //ActiveOrders[key] = order;
                    if (!ActiveOrders.ContainsKey(key))
                    {
                        ActiveOrders.Add(key, order);
                    }

                    // Chỉ add thêm, không update
                    if (!SimpleActiveOrders.ContainsKey(key))
                    {
                        SimpleActiveOrders.Add(key, new SimpleInfoOrder { FromEntrySignal = order.FromEntrySignal, Name = order.Name });
                            
                        CountOrder++;

                        if (EntrySignals.Contains(key))
                        {
                            CountEntrySignal++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LocalPrint("[OnOrderUpdate] - ERROR: ********" + e.Message + "************");
            }
            finally
            {
                //LocalPrint($"CountOrders: {ActiveOrders.Count}");
                LocalPrint(
                    $"[OnOrderUpdate] - key: [{key}], quantity: {quantity}, filled: {filled}, orderType: {order.OrderType}, orderState: {orderState}, " +
                    $"limitPrice: {limitPrice:N2}, stop: {stopPrice:N2}. Current number of active orders: {ActiveOrders.Count}");
            }
        }

        /// <summary>
        /// Giá stop loss
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
        protected abstract double GetStopLossPrice(T1 tradeAction, double setPrice);

        protected void LocalPrint(object val)
        {
            if (!AllowWriteLog)
            {
                return;
            }    
            
            if (val.GetType() == typeof(string))
            {
                Print($"[{LogPrefix}]-{Time?[0]}-" + val);
            }
            else
            {
                Print(val);
            }            
        }

        protected abstract double GetSetPrice(T1 tradeAction);

        /// <summary>
        /// Giải thuật nào sử dụng thì implement hàm này
        /// </summary>
        /// <param name="tradeAction"></param>
        /// <param name="setPrice"></param>
        /// <returns></returns>
        protected abstract double GetTargetPrice_Half(T1 tradeAction, double setPrice);

        protected abstract double GetTargetPrice_Full(T1 tradeAction, double setPrice);

        protected abstract T1 ShouldTrade();

        protected abstract void EnterOrder(T1 action);
        
        protected void EnterOrderPureUsingPrice(double priceToSet, double target, double stoploss, string signal, int quantity, bool isBuying, bool isSelling)
        {
            var text = isBuying ? "LONG" : "SHORT";

            var allowTrade = (isBuying && priceToSet < target) || (isSelling && priceToSet > target);

            if (allowTrade)
            {
                if (isBuying)
                {
                    EnterLongLimit(0, true, quantity, priceToSet, signal);
                    //EnterLongStopLimit(quantity, priceToSet, stoploss, signal);

                }
                else
                {
                    EnterShortLimit(0, true, quantity, priceToSet, signal);
                    //EnterShortStopLimit(quantity, priceToSet, stoploss, signal);
                }

                //SetStopLoss(signal, CalculationMode.Ticks, StopLossInTicks, false);
                SetStopLoss(signal, CalculationMode.Price, stoploss, false);

                SetProfitTarget(signal, CalculationMode.Price, target);

                LocalPrint($"Enter {text} for {quantity} contracts with signal [{signal}] at {priceToSet:N2}, stop loss: {stoploss:N2}, target: {target:N2}");
            }
        }        

        protected virtual void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string signal, int quantity, bool isBuying, bool isSelling)
        {
            var text = isBuying ? "LONG" : "SHORT";
            
            if (isBuying)
            {
                EnterLongLimit(0, true, quantity, priceToSet, signal);                

            }
            else
            {
                EnterShortLimit(0, true, quantity, priceToSet, signal);
                
            }

            SetStopLoss(signal, CalculationMode.Ticks, stoplossInTicks, false);            

            SetProfitTarget(signal, CalculationMode.Ticks, targetInTicks);

            LocalPrint($"Enter {text} for {quantity} contracts with signal [{signal}] at {priceToSet:N2}, stop loss ticks: {stoplossInTicks:N2}, target ticks: {targetInTicks:N2}");
            
        }

        /// <summary>
        /// Dịch chuyển 1 stop loss hoặc target order
        /// </summary>
        /// <param name="newPrice">Giá mới cần chuyển đến</param>
        /// <param name="order">Order</param>
        /// <param name="isGainStop">isGainStop = true: Profit order, isGainStop = false : Profit order</param>
        /// <param name="buyOrSell">Lệnh này là bán hay mua (dùng cho logger nên không quá quan trọng)</param>
        /// <param name="fromEntrySignal">Entry Signal</param>
        protected virtual void MoveTargetOrStopOrder(double newPrice, Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
        {
            try
            {
                if (isGainStop)
                {
                    SetProfitTarget(fromEntrySignal, CalculationMode.Price, newPrice);
                }
                else
                {
                    SetStopLoss(fromEntrySignal, CalculationMode.Price, newPrice, false);
                }

                var text = isGainStop ? "TARGET" : "LOSS";

                LocalPrint($"Dịch chuyển order [{order.Name}], id: {order.Id} ({text}), " +
                    $"{order.Quantity} contract(s) từ [{(isGainStop ? order.LimitPrice : order.StopPrice)}] " +
                    $"đến [{newPrice}] - {buyOrSell}");
            }
            catch (Exception ex)
            {
                LocalPrint($"[MoveTargetOrStopOrder] - ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Dịch chuyển stop loss. Có 2 trường hợp: (1) - Sau khi giá chạm vào target 1, kéo stop loss lên break even. 
        /// (2) - Khi giá gần chạm đến target 2, kéo stop loss lên gần với giá. 
        /// </summary>
        /// <param name="stopOrder"></param>
        /// <param name="updatedPrice"></param>
        /// <param name="filledPrice"></param>
        /// <param name="isBuying"></param>
        /// <param name="isSelling"></param>        
        protected virtual void MoveStopOrder(Order stopOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            double newPrice = -1;            
            var allowMoving = false;
            var stopOrderPrice = stopOrder.StopPrice;

            // Dịch stop loss lên break even 
            if (isBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice > filledPrice && stopOrderPrice + PointToMoveLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveLoss;
                    allowMoving = true; 
                }
                // Kéo về break even
                else if (SetBreakEvenManually && stopOrderPrice < filledPrice && filledPrice + 1 < updatedPrice)
                {
                    newPrice = filledPrice + 1;
                    allowMoving = true;
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
                // Kéo về break even
                else if (SetBreakEvenManually && stopOrderPrice > filledPrice && filledPrice - 1 > updatedPrice)
                {
                    newPrice = filledPrice - 1;
                    allowMoving = true;
                }
            }

            if (allowMoving)
            {
                LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{updatedPrice}]");                

                MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
            }
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
            LocalPrint($"[MoveTargetOrder] - {targetOrderPrice}");

            // Dịch stop gain nếu giá quá gần target            
            if (isBuying && updatedPrice + PointToMoveTarget > targetOrderPrice)
            {
                MoveTargetOrStopOrder(targetOrderPrice + PointToMoveTarget, targetOrder, true, "BUY", targetOrder.FromEntrySignal);

                StartMovingStoploss = true;
            }
            else if (isSelling && updatedPrice - PointToMoveTarget < targetOrderPrice)
            {
                MoveTargetOrStopOrder(targetOrderPrice - PointToMoveTarget, targetOrder, true, "SELL", targetOrder.FromEntrySignal);                

                StartMovingStoploss = true;
            }
        }

        /// <summary>
        /// Giá fill lệnh ban đầu 
        /// </summary>
        protected double FilledPrice = -1;

        protected DateTime FilledTime = DateTime.Now;

        protected HashSet<string> HalfPriceSignals { get; set; }

        /// <summary>
        /// All signals being used for this strategy, includes Half and Full size 
        /// </summary>
        protected HashSet<string> EntrySignals { get; set; }

        // Kéo stop loss/gain
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100)
            {
                return;
            }

            if (TradingStatus == TradingStatus.OrderExists)
            {
                MoveTargetAndStopOrdersWithNewPrice(updatedPrice, HalfPriceSignals);               
            }
        }

        protected abstract bool IsBuying { get; }

        protected abstract bool IsSelling { get; }

        private void MoveTargetAndStopOrdersWithNewPrice(double updatedPrice, HashSet<string> halfPriceSignals)
        {
            if (!AllowToMoveStopLossGain)
            {
                LocalPrint("NOT allow to move stop loss/gain");
                return;
            }

            try
            {
                // Order với half price
                var hasHalfPriceOder = SimpleActiveOrders.Values.Any(order => halfPriceSignals.Any(signal => signal == order.FromEntrySignal));

                if (hasHalfPriceOder) // Nếu còn order với half price (Chưa cắt half) --> Không nên làm gì
                {
                    return;
                }

                lock (lockOjbject)
                {
                    var stopOrders = ActiveOrders.Values.ToList()
                                        .Where(order => order.OrderType == OrderType.StopMarket || order.OrderType == OrderType.StopLimit)
                                        .ToList();

                    var targetOrders = ActiveOrders.Values.ToList()
                                        .Where(order => order.OrderState == OrderState.Working && order.OrderType == OrderType.Limit)
                                        .ToList();

                    var lenStop = stopOrders.Count;
                    for (var i = 0; i < lenStop; i++)
                    {
                        var stopOrder = stopOrders[i];
                        MoveStopOrder(stopOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
                    }

                    var lenTarget = targetOrders.Count;
                    for (var i = 0; i < lenTarget; i++)
                    {
                        var targetOrder = targetOrders[i];
                        MoveTargetOrder(targetOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
                    }
                }
            }
            catch (Exception e)
            {
                LocalPrint($"[OnMarketData] - ERROR: " + e.Message);
            }
        }

        protected abstract bool IsHalfPriceOrder(Order order);

        protected abstract bool IsFullPriceOrder(Order order);

        protected virtual Order GetOrderFromPendingList()
        {
            return ActiveOrders.FirstOrDefault().Value;
        }

        protected virtual void CloseExistingOrders()
        {
            LocalPrint($"[CloseExistingOrders]");
            var clonedList = ActiveOrders.Values.ToList().Where(c => c.OrderType == OrderType.Limit).ToList();
            var len = clonedList.Count;

            for (var i = 0; i < len; i++)
            {
                var order = clonedList[i];
                if (IsBuying)
                {
                    ExitLong(order.Quantity, "Close market", order.FromEntrySignal);
                }
                else if (IsSelling)
                {
                    ExitShort(order.Quantity, "Close market", order.FromEntrySignal);
                }
            }
        }

        /// <summary>
        /// Khi State từ Historical sang Realtime thì trong ActiveOrders có thể còn lệnh
        /// Nếu ChickenStatus == ChickenStatus.OrderExists thì các lệnh trong đó là các lệnh fake
        /// Nếu ChickenStatus == ChickenStatus.PendingFill thì phải transite các lệnh này sang chế độ LIVE
        /// </summary>
        protected virtual void TransitionOrdersToLive()
        {
            if (TradingStatus == TradingStatus.OrderExists)
            {
                LocalPrint($"Transition to live, clear all ActiveOrders");

                CloseExistingOrders();

                if (SimpleActiveOrders.Count > 0 || ActiveOrders.Count > 0)
                {
                    SimpleActiveOrders.Clear();
                    ActiveOrders.Clear();
                }

                LocalPrint($"Orders Count {SimpleActiveOrders.Count}");
            }
            else if (TradingStatus == TradingStatus.PendingFill)
            {
                LocalPrint($"Transition to live, convert all pending fill orders to realtime.");
                var clonedList = ActiveOrders.Values.ToList();
                var len = clonedList.Count;
                for (var i = 0; i < len; i++)
                {
                    var order = clonedList[i];
                    var newOrder = GetRealtimeOrder(order);

                    CancelOrder(newOrder);
                }
            }
        }

        /// <summary>
        /// Cancel các lệnh chờ khi có 1 trong các điều kiện sau: <br/>
        /// 1. Đợi quá lâu, hiện tại đợi 1h. <br/>
        /// 2. Vào lệnh trước 3:00pm nhưng hiện tại đã là sau 3:00. <br/>        
        /// </summary>
        /// <param name="filledOrderTime"></param>
        /// <returns></returns>
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


    }
}
