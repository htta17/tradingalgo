﻿#region Using declarations
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
	public abstract class Duck : Strategy, IATMStrategy
    {
        private const int DEMA_Period = 9;

        private const string Configuration_DuckParams_Name = "General configurations";

        private const string Configuration_ATMParams_Name = "ATM strategies";


        private string atmStrategyId = string.Empty;
        private string orderId = string.Empty;

        private double lastDEMA = -1;
        private TradingStatus DuckStatus = TradingStatus.Idle;

        /// <summary>
        /// Khoảng cách đảm bảo cho việc giá của stock chạy đúng hướng.
        /// </summary>
        private double WarranteeFee;

        /// <summary>
        /// If loss is more than [MaximumDayLoss], won't trade for that day
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Maximum Day Loss ($)", Order = 5, GroupName = StrategiesUtilities.Configuration_DailyPnL_Name)]
        public int MaximumDayLoss { get; set; }

        /// <summary>
        /// If gain is more than [StopWhenGain], won't trade for that day
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop Trading if Profit is ($)", Order = 6, GroupName = StrategiesUtilities.Configuration_DailyPnL_Name)]
        public int DailyTargetProfit { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Allow to move stop gain/loss", Order = 7, GroupName = Configuration_DuckParams_Name)]
        public bool AllowToMoveStopLossGain { get; set; } 

        /// <summary>
        /// Kiếm tra giờ trade(8:00-15:00)
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Check Trading Hour", Order = 8, GroupName = Configuration_DuckParams_Name)]
        public bool CheckTradingHour { get; set; } 

        [NinjaScriptProperty]
        [Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = Configuration_DuckParams_Name)]
        public string NewsTimeInput { get; set; } 

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Default ATM Strategy", Description = "Default ATM Strategy", Order = 4, 
            GroupName = Configuration_ATMParams_Name)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string FullSizeATMName { get; set; } 

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy", 
            Description = "Strategy sử dụng khi loss/gain more than a half", 
            Order = 4, GroupName = Configuration_ATMParams_Name)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfSizefATMName { get; set; } 

        private double PointToMoveGainLoss = 5;

        private List<int> NewsTimes = new List<int>();

        private OrderAction currentAction = OrderAction.Buy;
        private double filledPrice = -1;

        protected string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyDuck.txt");

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Using Bollinger bands + DEMA 5 mins, and EMA 29 + EMA 51 in 1 minute frame to trade.";
                Name = "Duck (Realtime EMA29/51 + Bollinger)";
                Calculate = Calculate.OnPriceChange;
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
                SetOrderQuantity = SetOrderQuantity.DefaultQuantity;
                DefaultQuantity = 5;

                // Set Properties		        
                FullSizeATMName = "Half_MNQ";
                HalfSizefATMName = "Half_MNQ";

                MaximumDayLoss = 260;
                DailyTargetProfit = 500;
                CheckTradingHour = true;
                AllowToMoveStopLossGain = true;

                NewsTimeInput = StrategiesUtilities.DefaultNewsTime;
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                StrategiesUtilities.CalculatePnL(this, Account, Print);

                try
                {
                    NewsTimes = NewsTimeInput.Split(',').Select(c => int.Parse(c)).ToList();
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }

                WarranteeFee = 3.0; //TickSize * TicksForWarantee;

                PointToMoveGainLoss = 5;

                // Load Current 
                if (File.Exists(FileName))
                {
                    try
                    {
                        atmStrategyId = File.ReadAllText(FileName);

                        Print($"WarranteeFee: {WarranteeFee}, PointToMoveGainLoss: {PointToMoveGainLoss}, current atmStrategyId: {atmStrategyId}");
                    }
                    catch (Exception e)
                    {
                        Print(e.Message);
                    }
                }

            }
            else if (State == State.DataLoaded)
            {
                var bollinger1 = Bollinger(1, 20);
                bollinger1.Plots[0].Brush = bollinger1.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1.Plots[1].Brush = Brushes.DeepPink;

                var bollinger2 = Bollinger(2, 20);
                bollinger2.Plots[0].Brush = bollinger2.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2.Plots[1].Brush = Brushes.DeepPink;

                AddChartIndicator(bollinger1);
                AddChartIndicator(bollinger2);
                AddChartIndicator(DEMA(9));
            }
        }

        protected virtual string LogInformationTitle { get; set; } = "[DUCK]";

        private void LocalPrint(object val)
        {
            if (val.GetType() == typeof(string))
            {
                Print($"[{LogInformationTitle}]{DateTime.Now}-{Time[0]}:: " + val);
            }
            else
            {
                Print(val);
            }
        }

        private void WriteDistance(string text)
        {
            Draw.TextFixed(
                this,
                "Distance",
                text,
                TextPosition.TopRight,
                Brushes.DarkBlue,            // Text color
                new SimpleFont("Arial", 12), // Font and size
                Brushes.DarkBlue,      // Background color
                Brushes.Transparent,      // Outline color
                0                         // Opacity (0 is fully transparent)
            );
        }

        private readonly object lockEnterOrder = new object();
        private void EnterOrder(OrderAction action, State state, TradingStatus status)
        {
            lock (lockEnterOrder)
            {
                bool isRealTime = state == State.Realtime;

                //var entryPrice = (ema29 + ema51) / 2; // Lấy theo EMA29/51
                var entryPrice = action == OrderAction.Buy ? lowerBB_5m : upperBB_5m;

                filledPrice = StrategiesUtilities.RoundPrice(entryPrice); 

                currentAction = action;

                LocalPrint($"Enter order {action} with status {status}");

                if (!isRealTime)
                {
                    return;
                }

                atmStrategyId = GetAtmStrategyUniqueId();
                orderId = GetAtmStrategyUniqueId();

                // If profit reaches half of daily goal or lose half of daily loss 
                var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                var reacHalf = todaysPnL <= -MaximumDayLoss / 2 || todaysPnL >= DailyTargetProfit / 2;
                var atmStragtegyName = reacHalf ? HalfSizefATMName : FullSizeATMName;

                try
                {
                    File.WriteAllText(FileName, atmStrategyId);
                }
                catch (Exception e)
                {
                    LocalPrint(e.Message);
                }

                var existingOrders = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                if (!existingOrders)
                {
                    // Enter a BUY/SELL order current price
                    AtmStrategyCreate(
                        action,
                        status == TradingStatus.OrderExists ? OrderType.Market : OrderType.Limit, // Market price if fill immediately
                        status == TradingStatus.OrderExists ? 0 : filledPrice,
                        0,
                        TimeInForce.Day,
                        orderId,
                        atmStragtegyName,
                        atmStrategyId,
                        (atmCallbackErrorCode, atmCallBackId) =>
                        {
                            if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                            {
                                LocalPrint($"Enter {action} - New StrategyID: {atmStrategyId} - New status: {DuckStatus}");
                            }
                        });
                }
            }
        }

        // Allow trade if: 
        //  	- Choose ShiftType is morning: Allow trade from 7:00am to 3:00pm
        //  	- Choose ShiftType is afternoon: Allow trade from 5:00pm to 11:00pm
        //  	- Choose ShiftType is overnight: Allow trade from 11:00pm to 7:00am
        //      - Avoid news
        private bool IsTradingHour()
        {
            if (!CheckTradingHour)
            {
                return true;
            }
            var time = ToTime(Time[0]);

            var newTime = NewsTimes.FirstOrDefault(c => StrategiesUtilities.NearNewsTime(time, c));

            if (newTime != 0)
            {
                LocalPrint($"News at {newTime} --> Not trading hour");
                return false;
            }

            return true;
        }

        private void MoveStopGainOrLoss(double __currentPrice)
        {
            if (!AllowToMoveStopLossGain)
            {
                LocalPrint("NOT allow to move stop loss or stop gain");
                return;
            }
            else if (string.IsNullOrEmpty(atmStrategyId))
            {
                LocalPrint("Don't have atmStrategyId information");
                return;
            }

            try
            {
                var updatedPrice = __currentPrice;

                var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains("Stop")).ToList();
                var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains("Target")).ToList();

                if (targetOrders.Count() != 1 || stopOrders.Count() != 1)
                {
                    return;
                }

                var targetOrder = targetOrders.FirstOrDefault();
                var stopOrder = stopOrders.FirstOrDefault();

                LocalPrint($"Check to move Stop gain or Loss - Current: Gain: {targetOrder.LimitPrice:F2}, Loss: {stopOrder.StopPrice:F2}, Point: {PointToMoveGainLoss},  Price: {updatedPrice} ");

                if (currentAction == OrderAction.Buy)
                {
                    // Dịch stop gain nếu giá quá gần target
                    if (updatedPrice + PointToMoveGainLoss > targetOrder.LimitPrice)
                    {
                        var newTarget = updatedPrice + PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            newTarget,
                            0,
                            targetOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển TARGET đến {newTarget} - BUY");
                    }

                    // Dịch chuyển stop loss nếu giá quá xa stop loss
                    if (stopOrder.StopPrice > filledPrice && stopOrder.StopPrice + PointToMoveGainLoss < updatedPrice)
                    {
                        var newStop = updatedPrice - PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            0,
                            newStop,
                            stopOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển LOSS đến {newStop} - BUY");
                    }
                }
                else if (currentAction == OrderAction.Sell)
                {
                    // Dịch stop gain nếu giá quá gần target
                    if (updatedPrice - PointToMoveGainLoss < targetOrder.LimitPrice)
                    {
                        var newTarget = updatedPrice - PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            newTarget,
                            0,
                            targetOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển TARGET đến {newTarget} - SELL");
                    }

                    // Dịch chuyển stop loss nếu giá quá xa stop loss
                    if (stopOrder.StopPrice < filledPrice && stopOrder.StopPrice - PointToMoveGainLoss > updatedPrice)
                    {
                        var newStop = updatedPrice + PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            0,
                            newStop,
                            stopOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển LOSS đến {newStop} - SELL");
                    }
                }
            }
            catch (Exception e)
            {
                LocalPrint(e.Message);
            }
        }

        private readonly object lockOjbject = new Object();

        protected double ema29_1m = -1;
        protected double ema51_1m = -1;
        protected double ema120_1m = -1;
        protected double ema89_1m = -1;
        protected double open_1m = -1;

        protected double upperBB_5m = -1;
        protected double lowerBB_5m = -1;
        protected double middleBB_5m = -1;

        protected double upperBB5m_Std2 = -1;
        protected double lowerBB5m_Std2 = -1;

        protected double lastUpperBB_5m = -1;
        protected double lastLowerBB_5m = -1;

        protected double currentPrice = -1;
        protected double currentDEMA = -1;
        protected double adx_5m = -1;
        protected double plusDI_5m = -1;
        protected double minusDI_5m = -1;

        private DateTime lastExecutionTime = DateTime.MinValue;
        private int executionCount = 0;

        double yesterdayHigh = -1;
        double yesterdayLow = -1;
        double yesterdayMiddle = -1;

        private double preMarketHigh = double.MinValue;
        private double preMarketLow = double.MaxValue;
        private double preMarketMiddle = 0;

        private double asianHigh = double.MinValue;
        private double asianLow = double.MaxValue;
        private double asianMiddle = double.MaxValue;
        protected override void OnBarUpdate()
        {
            if (CurrentBar < DEMA_Period)
            {
                return;
            }

            if (Bars.IsFirstBarOfSession)
            {
                yesterdayHigh = PriorDayOHLC().PriorHigh[0];
                yesterdayLow = PriorDayOHLC().PriorLow[0];
                yesterdayMiddle = (yesterdayHigh + yesterdayLow) / 2;
                Draw.HorizontalLine(this, "YesterdayHigh", yesterdayHigh, Brushes.Blue, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "YesterdayLow", yesterdayLow, Brushes.Blue, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "YesterdayMiddle", yesterdayMiddle, Brushes.Blue, DashStyleHelper.Dash, 1);

                preMarketHigh = double.MinValue;
                preMarketLow = double.MaxValue;
                asianHigh = double.MinValue;
                asianLow = double.MaxValue;
            }

            // Define pre-market time range (2:00AM to 8:30 AM CST)
            if (ToTime(Time[0]) >= 02_00_00 && ToTime(Time[0]) < 08_30_00)
            {
                preMarketHigh = Math.Max(preMarketHigh, High[0]);
                preMarketLow = Math.Min(preMarketLow, Low[0]);
                preMarketMiddle = (preMarketLow + preMarketHigh) / 2;
            }
            // Define Asian session time range (9:00 PM to 7:00 AM CST)
            if (ToTime(Time[0]) >= 21_00_00 || ToTime(Time[0]) < 07_00_00)
            {
                asianHigh = Math.Max(asianHigh, High[0]);
                asianLow = Math.Min(asianLow, Low[0]);
                asianMiddle = (asianHigh + asianLow) / 2;
            }

            // Draw lines at the open of the regular trading session
            if (ToTime(Time[0]) == 83000)
            {
                Draw.HorizontalLine(this, "PreMarketHigh", preMarketHigh, Brushes.Orange, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "PreMarketLow", preMarketLow, Brushes.Orange, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "PreMarketMiddle", preMarketMiddle, Brushes.Orange, DashStyleHelper.Dash, 1);

                Draw.HorizontalLine(this, "AsianHigh", asianHigh, Brushes.Green, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "AsianLow", asianLow, Brushes.Green, DashStyleHelper.Solid, 1);
                Draw.HorizontalLine(this, "AsianMiddle", asianMiddle, Brushes.Green, DashStyleHelper.Dash, 1);
            }            

            lock (lockOjbject)
            {
                try
                {
                    if (BarsInProgress == 2) // 1 minutes
                    {
                        ema29_1m = EMA(29).Value[0];
                        ema51_1m = EMA(51).Value[0];
                        ema120_1m = EMA(120).Value[0];
                        ema89_1m = EMA(89).Value[0];
                        open_1m = Open[0];
                        currentPrice = Close[0];
                        //LocalPrint($"BarsInProgress = 2 (1M): New prices for 1m: ema29: {ema29:F2}, ema51: {ema51:F2}.");
                    }
                    else if (BarsInProgress == 1) // 5 minues
                    {
                        var bollinger = Bollinger(1, 20);
                        var bollinger_Std2 = Bollinger(2, 20);

                        upperBB_5m = bollinger.Upper[0];
                        lowerBB_5m = bollinger.Lower[0];
                        middleBB_5m = bollinger.Middle[0];

                        upperBB5m_Std2 = bollinger_Std2.Upper[0];
                        lowerBB5m_Std2 = bollinger_Std2.Lower[0];

                        lastUpperBB_5m = bollinger.Upper[1];
                        lastLowerBB_5m = bollinger.Lower[1];

                        currentPrice = Close[0]; // = currentClose

                        currentDEMA = DEMA(DEMA_Period).Value[0];
                        lastDEMA = DEMA(DEMA_Period).Value[1];

                        adx_5m = ADX(14)[0];
                        plusDI_5m = DM(14).DiPlus[0];
                        minusDI_5m = DM(14).DiMinus[0];
                    }

                    if (State != State.Realtime)
                    {
                        return;
                    }
                    else if (DateTime.Now.Subtract(lastExecutionTime).TotalSeconds < 1)
                    {
                        return;
                    }
                    lastExecutionTime = DateTime.Now;

                    var isTradingHour = IsTradingHour();

                    executionCount++;
                    //LocalPrint($"OnBarUpdate execution Count: {executionCount}, Price: {Close[0]}");

                    if (DuckStatus == TradingStatus.Idle)
                    {
                        // Kiểm tra xem thực sự KHÔNG có lệnh nào hay không?
                        var hasActiveOrder = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                        if (hasActiveOrder) // Nếu có lệnh thì đổi lại status
                        {
                            DuckStatus = TradingStatus.OrderExists;
                        }
                        else
                        {
                            if (!isTradingHour)
                            {
                                return;
                            }

                            var reachDailyPnL = StrategiesUtilities.ReachMaxDayLossOrDayTarget(this, Account, MaximumDayLoss, DailyTargetProfit);

                            if (reachDailyPnL)
                            {
                                LocalPrint($"Reach daily gain/loss. Stop trading.");
                                return;
                            }

                            var enterLong = ShouldTrade(OrderAction.Buy);

                            var enterShort = enterLong == TradingStatus.Idle ? ShouldTrade(OrderAction.Sell) : TradingStatus.Idle;

                            LocalPrint($"I'm waiting - {DuckStatus} - Check to enter LONG: {enterLong}, check to ebter SHORT: {enterShort}");

                            if (enterLong == TradingStatus.PendingFill || enterLong == TradingStatus.OrderExists)  // Vào lệnh LONG market hoặc set lệnh LONG LIMIT
                            {
                                DuckStatus = enterLong;
                                EnterOrder(OrderAction.Buy, State, enterLong);
                                Print("Enter Long");
                            }
                            else if (enterShort == TradingStatus.PendingFill || enterShort == TradingStatus.OrderExists)
                            {
                                DuckStatus = enterShort;
                                EnterOrder(OrderAction.Sell, State, enterShort);
                                Print("Enter Short");
                            }
                            else if (enterLong == TradingStatus.WatingForCondition)
                            {
                                DuckStatus = enterLong;
                                currentAction = OrderAction.Buy;
                                Print("WaitingForGoodPrice to LONG");
                            }
                            else if (enterShort == TradingStatus.WatingForCondition)
                            {
                                DuckStatus = enterShort;
                                currentAction = OrderAction.Sell;
                                Print("WaitingForGoodPrice to SHORT");
                            }
                        }
                    }
                    else if (DuckStatus == TradingStatus.OrderExists) // Move stop gain/loss
                    {
                        // Check the order really exist
                        var hasActiveOrder = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                        if (!hasActiveOrder) // If no order exist --> Có thể vì 1 lý do nào đó (manually close, error, etc.) các lệnh đã bị đóng
                        {
                            DuckStatus = TradingStatus.Idle;
                            LocalPrint($"Chuyển về trạng thái IDLE từ OrderExist");
                        }
                        else
                        {
                            //LocalPrint($"{DuckStatus} - Current price: {currentPrice}");
                            MoveStopGainOrLoss(currentPrice);
                        }
                    }
                    else if (DuckStatus == TradingStatus.PendingFill)
                    {
                        // Move LIMIT order nếu giá di chuyển quá nhiều
                        var activeOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                        if (!activeOrders.Any()) // Nếu không có lệnh đợi filled
                        {
                            DuckStatus = TradingStatus.Idle;
                            LocalPrint($"Chuyển về trạng thái IDLE từ FillOrderPending");
                        }
                        else if (activeOrders.Any(c => c.Name.Contains("Target") || c.Name.Contains("Stop")))
                        {
                            DuckStatus = TradingStatus.OrderExists;
                            LocalPrint($"Chuyển về trạng thái ORDER_EXISTS từ FillOrderPending");
                        }
                        else // if (activeOrders.Any(c => c.Name.Contains("Entry")))
                        {
                            UpdatePendingOrder(activeOrders.FirstOrDefault());
                        }
                    }
                    else if (DuckStatus == TradingStatus.WatingForCondition)
                    {
                        var hasActiveOrder = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                        if (hasActiveOrder) // If no order exist --> Có thể vì 1 lý do nào đó (manually close, error, etc.) các lệnh đã bị đóng
                        {
                            DuckStatus = TradingStatus.OrderExists;
                            LocalPrint($"Chuyển sang trạng thái ORDER_EXISTS từ WaitingForGoodPrice");
                        }
                        else
                        {
                            // Cập nhật luôn trạng thái mới
                            var newStatus = WaitForTradeCondition();

                            //LocalPrint($"WaitingForGoodPrice - NewStatus: {newStatus}");
                            var existingOrders = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                            if (existingOrders)
                            {
                                DuckStatus = TradingStatus.OrderExists;
                                LocalPrint($"Chuyển sang trạng thái ORDER_EXISTS từ WaitingForGoodPrice");
                            }
                            else if (!existingOrders && (newStatus == TradingStatus.OrderExists || newStatus == TradingStatus.PendingFill))
                            {
                                EnterOrder(currentAction, State, newStatus);
                            }

                            DuckStatus = newStatus;
                        }
                    }
                }
                catch (Exception e)
                {
                    LocalPrint(e.Message);
                }
            }
        }

        /*
		 * Hàm này sử dụng để trả về Idle hoặc trade theo Trending, khi không có điều kiện về đánh ngược
		 */
        protected virtual TradingStatus ConditionIfCannotFindCross(OrderAction orderAction)
        {
            return TradingStatus.Idle;
        }

        /**
		* Hàm này chỉ làm việc khi DuckStatus là Idle
		*/
        protected virtual TradingStatus ShouldTrade(OrderAction action)
        {
            /*
			* Điều kiện vào lệnh: 
			* 1. Điều kiện về B-line (DEMA): lastDEMA ở ngoài và currentDEMA rất gần upper (nếu SELL) hoặc lower (nếu BUY) 
			* 2. Nếu điều kiện 1 được thỏa mãn
			*/

            if (DuckStatus != TradingStatus.Idle)
            {
                return DuckStatus;
            }

            var minDistance = Math.Min(Math.Abs(currentDEMA - lowerBB_5m), Math.Abs(upperBB_5m - currentDEMA));

            if (action == OrderAction.Buy)
            {
                //var bigCloudVal = Math.Min(ema89, ema120);
                var bigCloudVal = ema120_1m;

                var foundCross = lastDEMA < lastLowerBB_5m && // last DEMA ở dưới BB 
                    ((currentDEMA < lowerBB_5m && lowerBB_5m - currentDEMA <= WarranteeFee) || currentDEMA >= lowerBB_5m);  // DEMA ở dưới BB nhưng cách BB <=3 pts, hoặc DEMA đã >= BB - tức là đã đi vào trong

                WriteDistance($"Distance: {minDistance:F2}, lastDEMA: {lastDEMA:F2}, , LowerBB: {lowerBB_5m:F2}, currentDEMA: {currentDEMA:F2} --> CROSS: {foundCross}");

                if (!foundCross)
                {
                    //LocalPrint($"NOT found cross BUY, lastDEMA: {lastDEMA:F2} lastUpperBB5m: {lastLowerBB_5m:F2}, currentDEMA: {currentDEMA:F2}, lowerBB5m:{lowerBB_5m:F2}, WarranteeFee: {WarranteeFee:F2}");
                    return ConditionIfCannotFindCross(action); // Tiếp tục trạng thái hiện tại
                }
                else if (open_1m < Math.Max(ema29_1m, ema51_1m)) // Found cross, nhưng Open của nến 1 phút vẫn ở dưới EMA29/51)
                {
                    LocalPrint($"Found cross BUY, but open1m {open_1m:F2} < Math.Max({ema29_1m:F2}, {ema51_1m:F2})");
                    return TradingStatus.WatingForCondition; // Đợi khi nào có nến 1 phút vượt qua EMA29/51 thì set lệnh
                }
                else if (adx_5m < 22)
                {
                    LocalPrint("Vào lệnh BUY theo giá MARKET do có ADX < 22 (Sizeway)");
                    return TradingStatus.OrderExists;
                }
                else if (currentPrice < bigCloudVal && bigCloudVal - currentPrice >= 10)
                {
                    LocalPrint("Vào lệnh BUY theo giá MARKET do cách xa EMA120");
                    return TradingStatus.OrderExists;
                }
                /*
				* Điều kiện có sẵn: open1m > Math.Max(ema29, ema51). 
				* adx_5m > 25: Đang có xu hướng MẠNH 
				* minusDI > plusID: Xu hướng GIẢM
				* Như vậy, (adx_5m > 25 && minusDI > plusID) nghĩa là xu hướng vừa GIẢM mạnh, do đó mua vào là ngược trend, RISKY
				* Math.Max(ema29, ema51) < Math.Min(ema89, ema120) && Math.Min(ema89, ema120) - Math.Max(ema29, ema51) < 10 là 
				*/
                else if (adx_5m > 25 && minusDI_5m > plusDI_5m &&
                        Math.Max(ema29_1m, ema51_1m) < bigCloudVal && bigCloudVal - Math.Max(ema29_1m, ema51_1m) < 10)
                {
                    LocalPrint("Chờ fill lệnh BUY");
                    return TradingStatus.PendingFill;
                }
                else
                {
                    LocalPrint($"Found cross BUY, but too RISKY");
                    /*
					LocalPrint("Vào lệnh BUY theo giá MARKET");
					return DuckStatus.OrderExist;
					*/
                }
            }
            else if (action == OrderAction.Sell)
            {
                //var bigCloudVal = Math.Max(ema89, ema120);
                var bigCloudVal = ema120_1m;

                var foundCross = lastDEMA > lastUpperBB_5m && // DEMA ở trên BB 
                    ((currentDEMA > upperBB_5m && currentDEMA - upperBB_5m <= WarranteeFee) || currentDEMA <= upperBB_5m); // DEMA đã vào trong BB 					

                WriteDistance($"Distance: {minDistance:F2}, lastDEMA: {lastDEMA:F2}, , UpperBB: {upperBB_5m:F2}, currentDEMA: {currentDEMA:F2} --> CROSS: {foundCross}");

                if (!foundCross)
                {
                    //LocalPrint($"NOT found cross SELL, lastDEMA: {lastDEMA:F2} lastUpperBB5m: {lastUpperBB_5m:F2}, currentDEMA: {currentDEMA:F2}, upperBB5m:{upperBB_5m:F2}, WarranteeFee: {WarranteeFee:F2}");
                    return ConditionIfCannotFindCross(action);
                }
                else if (open_1m > Math.Min(ema29_1m, ema51_1m)) // foundCross = true, nhưng open của nến 1 phút vẫn nằm trên EMA29/51 (chưa vượt qua được)
                {
                    LocalPrint($"Found cross SELL, but open1m {open_1m:F2} > Math.Min({ema29_1m:F2}, {ema51_1m:F2})");
                    return TradingStatus.WatingForCondition;
                }
                else if (adx_5m < 22)
                {
                    LocalPrint("Vào lệnh SELL theo giá MARKET do có ADX < 22 (Sizeway)");
                    return TradingStatus.OrderExists;
                }
                else if (currentPrice > bigCloudVal && currentPrice - bigCloudVal >= 10)
                {
                    LocalPrint("Vào lệnh SELL theo giá MARKET, price xa so với EMA 120");
                    return TradingStatus.OrderExists;
                }
                else if (adx_5m > 25 && minusDI_5m < plusDI_5m && Math.Min(ema29_1m, ema51_1m) > bigCloudVal && bigCloudVal - Math.Min(ema29_1m, ema51_1m) < 10)
                {
                    LocalPrint("Chờ fill lệnh SELL");
                    return TradingStatus.PendingFill;
                }
                else
                {
                    LocalPrint($"Found cross SELL, but too RISKY");
                    /*
                    LocalPrint("Vào lệnh SELL theo giá MARKET");
                    return DuckStatus.OrderExist;
					*/
                }
            }

            return TradingStatus.Idle;
        }


        // Hàm này chỉ làm việc với DuckStatus hiện tại là DuckStatus.WaitingForGoodPrice
        protected virtual TradingStatus WaitForTradeCondition()
        {
            if (DuckStatus != TradingStatus.WatingForCondition)
            {
                return DuckStatus;
            }

            var minDistance = Math.Min(Math.Abs(currentDEMA - lowerBB_5m), Math.Abs(upperBB_5m - currentDEMA));
            WriteDistance($"Distance: {minDistance:F2}");

            if (currentAction == OrderAction.Buy)
            {
                var bigCloudVal = ema120_1m;

                var foundCross = lastDEMA < lastLowerBB_5m && // last DEMA ở dưới BB 
                    ((currentDEMA < lowerBB_5m && lowerBB_5m - currentDEMA <= WarranteeFee) || currentDEMA >= lowerBB_5m);

                // DEMA vẫn nằm dưới lowerBB5m và ngày càng cách xa lowerBB5m, (BEARISH)
                var priceWentFarDown = lowerBB_5m - currentDEMA >= WarranteeFee + 8 * TickSize;

                // Đã đi theo chiều BULLISH quá xa
                var priceWentFarUp =
                            // DEMA trước và hiện tại đều đã ở trên Bollinger Lower Band
                            lastDEMA > lastLowerBB_5m && currentDEMA > lowerBB_5m
                            // Nhưng giá chưa chạm đến vùng EMA120 
                            && currentPrice < bigCloudVal
                            // Và khoảng cách là <7 điểm, quá bé
                            && bigCloudVal - currentPrice < 7
                            // Và ADX quá lớn (Trend mạnh) 
                            && adx_5m > 27;

                if (priceWentFarDown || priceWentFarUp || !foundCross)
                {
                    LocalPrint($"SETUP hết đẹp, quay trở về trạng thái IDLE");
                    return ConditionIfCannotFindCross(currentAction); // Hết setup đẹp, tiếp tục trở về Idle để đợi
                }
                else if (open_1m < Math.Max(ema29_1m, ema51_1m)) // Found cross, nhưng Open của nến 1 phút vẫn ở dưới EMA29/51)
                {
                    LocalPrint($"Continue waiting, open1m {open_1m:F2} < Math.Max({ema29_1m:F2}, {ema51_1m:F2})");
                    return TradingStatus.WatingForCondition; // Tiếp tục chờ đợi
                }
                else if (adx_5m < 22)
                {
                    LocalPrint("Vào lệnh BUY theo giá MARKET do có ADX < 22 (Sizeway)");
                    return TradingStatus.OrderExists;
                }
                else if (//open1m >= Math.Max(ema29, ema51) && 
                    currentPrice < bigCloudVal && bigCloudVal - currentPrice >= 10)
                {
                    LocalPrint("Vào lệnh BUY theo giá MARKET");
                    return TradingStatus.OrderExists;
                }
                else if (adx_5m > 25 && minusDI_5m > plusDI_5m &&
                        Math.Max(ema29_1m, ema51_1m) < bigCloudVal && bigCloudVal - Math.Max(ema29_1m, ema51_1m) < 10)
                {
                    LocalPrint("Chờ fill lệnh BUY");
                    return TradingStatus.PendingFill;
                }
                else
                {
                    LocalPrint($"Found cross BUY, but too RISKY");
                    /*
					LocalPrint("Vào lệnh BUY theo giá MARKET");
					return DuckStatus.OrderExist;
					*/
                }
            }
            else if (currentAction == OrderAction.Sell)
            {
                var bigCloudVal = ema120_1m;

                var foundCross = lastDEMA > lastUpperBB_5m && // DEMA ở trên BB 
                    ((currentDEMA > upperBB_5m && currentDEMA - upperBB_5m <= WarranteeFee) || currentDEMA <= upperBB_5m); // DEMA đã vào trong BB

                // DEMA vẫn nằm trên upperBB5m và ngày càng cách xa upperBB5m, (BULLISH)
                var priceWentFarUp = currentDEMA - upperBB_5m >= WarranteeFee + 8 * TickSize;

                // Đã đi theo chiều BEARISH quá xa
                var priceWentFarDown =
                            // DEMA trước và hiện tại đều đã ở dưới Bollinger Lower Band
                            lastDEMA < lastUpperBB_5m && currentDEMA > upperBB_5m
                            // Nhưng giá chưa chạm đến vùng EMA120 
                            && currentPrice > bigCloudVal
                            // Và khoảng cách là <7 điểm, quá bé
                            && currentPrice - bigCloudVal < 7
                            // Và ADX quá lớn (Trend mạnh) 
                            && adx_5m > 27;

                if (priceWentFarDown || priceWentFarUp || !foundCross)

                    if (currentDEMA > upperBB_5m && currentDEMA - upperBB_5m >= WarranteeFee + 8 * TickSize) // DEMA vẫn nằm trên upperBB5m và ngày càng cách xa upperBB5m
                    {
                        LocalPrint($"SETUP hết đẹp, quay trở về trạng thái IDLE");
                        return ConditionIfCannotFindCross(currentAction); // Hết setup đẹp, tiếp tục trở về Idle để đợi
                    }
                    else if (open_1m > Math.Min(ema29_1m, ema51_1m)) // foundCross = true, nhưng open của nến 1 phút vẫn nằm trên EMA29/51 (chưa vượt qua được)
                    {
                        LocalPrint($"Found cross SELL, but open1m {open_1m:F2} > Math.Min({ema29_1m:F2}, {ema51_1m:F2})");
                        return TradingStatus.WatingForCondition;
                    }
                    else if (adx_5m < 22)
                    {
                        LocalPrint("Vào lệnh SELL theo giá MARKET do có ADX < 22 (Sizeway)");
                        return TradingStatus.OrderExists;
                    }
                    else if (currentPrice > Math.Max(ema89_1m, ema120_1m) && currentPrice - Math.Max(ema89_1m, ema120_1m) >= 10)
                    {// --> Cho phép vào lệnh
                        LocalPrint("Vào lệnh SELL theo giá MARKET");
                        return TradingStatus.OrderExists;
                    }
                    else if (adx_5m > 25 && minusDI_5m < plusDI_5m && Math.Min(ema29_1m, ema51_1m) > bigCloudVal && bigCloudVal - Math.Min(ema29_1m, ema51_1m) < 10)
                    {
                        LocalPrint("Chờ fill lệnh SELL");
                        return TradingStatus.PendingFill;
                    }
                    else
                    {
                        LocalPrint($"Found cross SELL, but too RISKY");
                        /*
                        LocalPrint("Vào lệnh SELL theo giá MARKET");
                        return DuckStatus.OrderExist;
                        */
                    }
            }

            return TradingStatus.WatingForCondition;
        }

        private void UpdatePendingOrder(Order pendingOrder)
        {
            if (DuckStatus != TradingStatus.PendingFill)
            {
                return;
            }
            if (pendingOrder == null)
            {
                foreach (var order in Account.Orders)
                {
                    LocalPrint($"Debug - {order.OrderState}");
                }
                DuckStatus = TradingStatus.Idle;
                LocalPrint($"ERROR: DuckStatus không đúng. Reset status. - Current status: {DuckStatus}");
                return;
            }

            //LocalPrint($"UpdatePendingOrder - Order {pendingOrder.OrderAction} - Price: {pendingOrder.LimitPrice} ");

            if (string.IsNullOrEmpty(atmStrategyId) || string.IsNullOrEmpty(orderId)) // We don't have any information
            {
                return;
            }

            var cancelByPrice =
                (currentAction == OrderAction.Buy && currentPrice > upperBB5m_Std2) ||
                (currentAction == OrderAction.Sell && currentPrice < lowerBB5m_Std2);

            //LocalPrint($"currentAction: {currentAction}, currentPriceBar5m: {currentPrice:F2}, upperBB5m_Std2: {upperBB5m_Std2:F2}, lowerBB5m_Std2: {lowerBB5m_Std2:F2}");

            if (cancelByPrice || (Time[0] - pendingOrder.Time).TotalMinutes > 60) // Cancel lệnh vì market đóng cửa, vì giá đi quá cao hoặc vì
            {
                AtmStrategyCancelEntryOrder(orderId);

                orderId = null;
                atmStrategyId = null;
                DuckStatus = TradingStatus.Idle;

                LocalPrint($"Chờ quá lâu, cancel lệnh. Status {DuckStatus} now. ");
            }
            else
            {
                //var entryPrice = (ema29 + ema51) / 2.0; 
                var entryPrice = currentAction == OrderAction.Buy ? lowerBB_5m : upperBB_5m;
                var newPrice = StrategiesUtilities.RoundPrice(entryPrice); 

                var shouldMoveBuy = currentAction == OrderAction.Buy
                    && newPrice > pendingOrder.LimitPrice
                    && newPrice - PointToMoveGainLoss > pendingOrder.LimitPrice;

                var shouldMoveSell = currentAction == OrderAction.Sell
                    && newPrice < pendingOrder.LimitPrice
                    && newPrice + PointToMoveGainLoss < pendingOrder.LimitPrice;

                if (shouldMoveBuy || shouldMoveSell)
                {
                    filledPrice = newPrice;
                    var changedSuccessdful = AtmStrategyChangeEntryOrder(newPrice, 0, orderId);
                    var text = changedSuccessdful ? "success" : "UNsuccess";

                    LocalPrint($"Update LIMIT {text} {currentAction} price: ${newPrice:F2}");
                }
            }
            /*
			End of UpdatePendingOrder
			*/
        }
        

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            Print($"OnExecutionUpdate: {execution.Name}");

            base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);
        }

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            Print($"OnOrderUpdate: {order.Name}");

            base.OnOrderUpdate(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, comment);
        }
    }
}
