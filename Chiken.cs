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
using System.IO;
using NinjaTrader.Custom.Strategies;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public enum WayToTrade
    {
        EasyFill_HighRisk,
        LowRisk_HardFill,
        MiddleRisk_MiddleLine,
        SantaOnly,
        BollingerBandOnly
    }

    public enum ChickenStatus
    {
        Idle, // Đang không có lệnh 
        PendingFill, // Lệnh đã submit nhưng chưa được fill do giá chưa đúng
        OrderExists  // Lệnh đã được filled 
    }

    public class Chicken : Strategy
    {
        private int DEMA_Period = 9;

        private double ema29 = -1;
        private double ema51 = -1;
        private double upper = -1;
        private double lower = -1;
        private double middle = -1;

        private double low5m = -1;
        private double high5m = -1;

        OrderAction currentAction = OrderAction.Buy;

        private double filledPrice = -1;

        private ChickenStatus DoubleBBStatus = ChickenStatus.Idle;

        [NinjaScriptProperty]
        [Display(Name = "Choose way to trade", Order = 3, GroupName = "Parameters")]
        public WayToTrade WayToTrade { get; set; } = WayToTrade.EasyFill_HighRisk;

        /// <summary>
        /// ATM name for live trade. 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "ATM Strategy", Order = 4, GroupName = "Parameters")]
        public string FullATMName { get; set; } = "Default_MNQ";

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy", Description = "Strategy sử dụng khi loss/gain more than a half", Order = 4, GroupName = "Importants Configurations")]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfATMName { get; set; } = "Half_MNQ";

        /// <summary>
        /// If loss is more than [MaximumDayLoss], won't trade for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Maximum Day Loss ($)", Order = 5, GroupName = "Parameters")]
        public int MaximumDayLoss { get; set; } = 400;

        /// <summary>
        /// If gain is more than [StopWhenGain], won't trade for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop Trading if Profit is ($)", Order = 6, GroupName = "Parameters")]
        public int StopGainProfit { get; set; } = 700;

        [NinjaScriptProperty]
        [Display(Name = "Check Trading Hour", Order = 7, GroupName = "Parameters")]
        public bool CheckTradingHour { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Allow to move stop loss/profit target", Order = 8, GroupName = "Parameters")]
        public bool AllowToMoveStopLossGain { get; set; } = true;

        /*
        [NinjaScriptProperty]
        [Display(Name = "Shift Type (AM/PM/Night)", Order = 9, GroupName = "Parameters")]
        public ShiftType ShiftType { get; set; } = ShiftType.Moning_0700_1500;
        */

        [NinjaScriptProperty]
        [Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
        public string NewsTimeInput { get; set; } = "0830";

        private List<int> NewsTimes = new List<int>();

        private readonly string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategy.txt");

        private double PointToMoveGainLoss = -1;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Play on 5 minutes frame.";
                Name = "Chicken";
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
                SetOrderQuantity = SetOrderQuantity.DefaultQuantity;
                DefaultQuantity = 5;

                // Set Properties
                FullATMName = "Default_MNQ";
                HalfATMName = "Half_MNQ";
                WayToTrade = WayToTrade.EasyFill_HighRisk;

                MaximumDayLoss = 400;
                StopGainProfit = 700;
                CheckTradingHour = true;
                AllowToMoveStopLossGain = true;

                // ShiftType = ShiftType.Moning_0700_1500;
                NewsTimeInput = "0830";
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                try
                {
                    NewsTimes = NewsTimeInput.Split(',').Select(c => int.Parse(c)).ToList();
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }

                // Load current atmStrategyId				
                if (File.Exists(FileName))
                {
                    try
                    {
                        atmStrategyId = File.ReadAllText(FileName);

                        Print($"Current ATMStrategy: {atmStrategyId}");
                    }
                    catch (Exception ex)
                    {
                        Print(ex.Message);
                    }
                }

                PointToMoveGainLoss = Instrument.FullName.Contains("MNQ") ? 5 : 0.7;
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

        private bool ShouldTrade(OrderAction action,
            double upper,
            double lower,
            double middle,
            double currentPrice,
            double currentOpen,
            double lastOpen,
            double lastClose,
            double currentDEMA,
            double lastDEMA)
        {
            /*
			* Điều kiện để trade (SHORT) 
			* 1. currentDEMA < upper & lastDEMA >= upper
			* 2. currentPrice > lower && currentPrice < upper
			*/

            if (currentPrice > lower && currentPrice < upper)
            {
                if (action == OrderAction.Sell)
                {
                    return lastDEMA > upper && currentDEMA <= upper; //&& lastOpen > upper;
                }
                else if (action == OrderAction.Buy)
                {
                    return lastDEMA < lower && currentDEMA >= lower; //&& lastOpen < lower;
                }
            }

            return false;
        }

        private void CalculatePnL()
        {
            try
            {
                var profitloss = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

                Draw.TextFixed(
                        this,
                        "RunningAcc",
                        $"Run on: {Account.Name} - Net Liquidation: {Account.Get(AccountItem.NetLiquidation, Currency.UsDollar):C2}",
                        TextPosition.BottomLeft,
                        Brushes.DarkBlue,            // Text color
                        new SimpleFont("Arial", 12), // Font and size
                        Brushes.DarkBlue,      // Background color
                        Brushes.Transparent,      // Outline color
                        0                         // Opacity (0 is fully transparent)
                    );
            }
            catch (Exception e)
            {
                Print(e.Message);
            }
        }

        private double GetSetPrice(double upper, double lower, double ema29, double ema51, WayToTrade chooseWay, OrderAction orderAction)
        {
            double price = -1;
            if (orderAction == OrderAction.Buy || orderAction == OrderAction.BuyToCover)
            {
                if (chooseWay == WayToTrade.EasyFill_HighRisk)
                {
                    price = Math.Max(lower, Math.Max(ema29, ema51));
                }
                else if (chooseWay == WayToTrade.LowRisk_HardFill)
                {
                    price = Math.Min(lower, Math.Min(ema29, ema51));
                }
                else if (chooseWay == WayToTrade.MiddleRisk_MiddleLine)
                {
                    price = (lower + ema29 + ema51) / 3;
                }
                else if (chooseWay == WayToTrade.SantaOnly)
                {
                    price = (ema29 + ema51) / 2;
                }
                else
                {
                    price = lower;
                }
            }
            else // if (orderAction == OrderAction.Sell || orderAction == OrderAction.SellShort)
            {
                if (chooseWay == WayToTrade.EasyFill_HighRisk)
                {
                    price = Math.Min(upper, Math.Min(ema29, ema51));
                }
                else if (chooseWay == WayToTrade.LowRisk_HardFill)
                {
                    price = Math.Max(upper, Math.Max(ema29, ema51));
                }
                else if (chooseWay == WayToTrade.MiddleRisk_MiddleLine)
                {
                    price = (upper + ema29 + ema51) / 3;
                }
                else if (chooseWay == WayToTrade.SantaOnly)
                {
                    price = (ema29 + ema51) / 2;
                }
                else
                {
                    price = upper;
                }
            }

            return Math.Round(price * 4, MidpointRounding.AwayFromZero) / 4.0;
        }

        private void LocalPrint(object val)
        {
            if (val.GetType() == typeof(string))
            {
                Print($"[CHICKEN]-{Time[0]}-" + val);
            }
            else
            {
                Print(val);
            }
        }

        private string atmStrategyId = "";
        private string orderId = "";

        private void EnterOrder(OrderAction action, State state, double entryPrice, double upper, double lower, double ema29, double ema51)
        {
            double priceToSet = GetSetPrice(upper, lower, ema29, ema51, WayToTrade, action);

            // Set new status
            DoubleBBStatus = ChickenStatus.PendingFill;
            currentAction = action;

            if (State == State.Realtime)
            {
                atmStrategyId = GetAtmStrategyUniqueId();
                orderId = GetAtmStrategyUniqueId();
                filledPrice = priceToSet;

                try
                {
                    File.WriteAllText(FileName, atmStrategyId);
                }
                catch (Exception ex)
                {
                    LocalPrint(ex.Message);
                }

                // If profit reaches half of daily goal or lose half of daily loss 
                var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                var reacHalf = todaysPnL <= -MaximumDayLoss / 2 || todaysPnL >= StopGainProfit / 2;
                var atmStragtegyName = reacHalf ? HalfATMName : FullATMName;

                // Enter a BUY/SELL order current price
                AtmStrategyCreate(action,
                    OrderType.Limit,
                    priceToSet,
                    0,
                    TimeInForce.Day,
                    orderId,
                    atmStragtegyName,
                    atmStrategyId,
                    (atmCallbackErrorCode, atmCallBackId) => {
                        if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                        {
                            Print($"Set a {action} at {priceToSet:F2}. Waiting to be filled.");
                        }
                    });
            }
            else
            {
                //SetStopLoss(CalculationMode.Ticks, StopLossTicks);
                //SetProfitTarget(CalculationMode.Ticks, StopGainTicks);
                //currentOrder = action == OrderAction.Buy ? EnterLongLimit(priceToSet) : EnterShortLimit(priceToSet);

                /*
				if (currentOrder != null)
				{
					 LocalPrint($"Submitted order {action} at {currentOrder.LimitPrice} ");
				}
				*/
            }

            CalculatePnL();
        }

        // Kéo stop loss/gain
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            if (DoubleBBStatus == ChickenStatus.OrderExists) // Điều chỉnh stop gain/loss 
            {
                if (!AllowToMoveStopLossGain)
                {
                    LocalPrint("NOT allow to move stop loss/gain");
                    return;
                }
                else if (string.IsNullOrEmpty(atmStrategyId))
                {
                    LocalPrint("atmStrategyId is null");
                    return;
                }

                try
                {
                    var updatedPrice = marketDataUpdate.Price;

                    if (updatedPrice < 100)
                    {
                        LocalPrint($"MarketDataUpdate, price {updatedPrice} - SYSTEM ERROR");
                        return;
                    }

                    var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains("Stop")).ToList();
                    var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains("Target")).ToList();

                    if (targetOrders.Count() != 1 || stopOrders.Count() != 1)
                    {
                        return;
                    }

                    var targetOrder = targetOrders.FirstOrDefault();
                    var stopOrder = stopOrders.FirstOrDefault();

                    if (currentAction == OrderAction.Buy)
                    {
                        var shouldMoveGain = updatedPrice + PointToMoveGainLoss > targetOrder.LimitPrice;
                        LocalPrint($"Condition to move gain: Updatedprice: {updatedPrice}, PointToMoveGainLoss: {PointToMoveGainLoss}, target: {targetOrder.LimitPrice} --> Should move: {updatedPrice + PointToMoveGainLoss > targetOrder.LimitPrice}");

                        // Dịch stop gain nếu giá quá gần target
                        if (shouldMoveGain)
                        {
                            var newTarget = targetOrder.LimitPrice + PointToMoveGainLoss;

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
                            var newTarget = targetOrder.LimitPrice - PointToMoveGainLoss;

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
            else if (DoubleBBStatus == ChickenStatus.PendingFill)
            {
                // Hiện tại chưa lấy được các thông số (EMA, Bollinger, etc.) real time nên chưa làm gì.

            }
        }

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
            LocalPrint($"OnOrderUpdate - Time: {time}, DateTime: {DateTime.Now} orderState: {orderState}, limitPrice: {limitPrice:F2}, stop: {stopPrice:F2}, status: {DoubleBBStatus}");

            if (DoubleBBStatus == ChickenStatus.PendingFill)
            {
                if (orderState == OrderState.Filled)
                {
                    DoubleBBStatus = ChickenStatus.OrderExists;
                    LocalPrint($"Filled {order.Account.Name} at {limitPrice:F2} ({order.LimitPrice}), stop {stopPrice}");
                }
            }
            else if (DoubleBBStatus == ChickenStatus.OrderExists)
            {
                if (orderState == OrderState.Filled || orderState == OrderState.Cancelled)
                {
                    DoubleBBStatus = ChickenStatus.Idle;
                    LocalPrint($"{orderState} {order.Account.Name} at {limitPrice:F2} ({order.LimitPrice}), stop {stopPrice}");
                }
            }
        }

        private bool NearNewsTime(int time, int newsTime)
        {
            // newsTime format: 0700,0830,1300
            var minute = newsTime % 100;
            var hour = newsTime / 100;

            var left = -1;
            var right = -1;

            if (minute >= 5 && minute <= 54) // time is 0806 --> no trade from 080100 to 081100
            {
                left = hour * 10000 + (minute - 5) * 100;
                right = hour * 10000 + (minute + 5) * 100;
            }
            else if (minute < 5) // time is 0802 --> no trade from 075700 to 080700
            {
                left = (hour - 1) * 10000 + (minute + 55) * 100;
                right = hour * 10000 + (minute + 5) * 100;
            }
            else // minute >= 55 - time is 0856 --> No trade from 085100 to 090100
            {
                left = hour * 10000 + (minute - 5) * 100;
                right = (hour + 1) * 10000 + (minute - 55) * 100;
            }

            return left < time && time < right;
        }

        // Check if current time is still from 8:05:10 AM to 3:00 PM.
        private bool IsTradingHour()
        {
            if (!CheckTradingHour)
            {
                return true;
            }
            var time = ToTime(Time[0]);

            var newTime = NewsTimes.FirstOrDefault(c => NearNewsTime(time, c));

            if (newTime != 0)
            {
                LocalPrint($"News at {newTime} --> Not trading hour");
                return false;
            }

            /*
            if (ShiftType == ShiftType.Moning_0700_1500 && (time < 070000 || time > 150000))
            {
                LocalPrint($"Time: {time} - Shift {ShiftType} --> Not trading hour");
                return false;
            }
            else if (ShiftType == ShiftType.Afternoon_1700_2300 && (time < 170000 || time > 230000))
            {
                LocalPrint($"Time: {time} - Shift {ShiftType} --> Not trading hour");
                return false;
            }
            else if (ShiftType == ShiftType.Night_2300_0700 && (time >= 070000 && time <= 230000))
            {
                LocalPrint($"Time: {time} - Shift {ShiftType} --> Not trading hour");
                return false;
            }
			*/

            return true;
        }

        /// <summary>
        /// Nếu đã đủ lợi nhuận hoặc đã bị thua lỗ quá nhiều thì dừng (bool reachDailyPnL, double totalPnL, bool isWinDay)
        /// </summary>
        /// <returns></returns>
        private bool ReachMaxDayLossOrDayTarget()
        {
            // Calculate today's P&L
            double todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachDayLimit = todaysPnL <= -MaximumDayLoss || todaysPnL >= StopGainProfit;

            var additionalText = reachDayLimit ? ". DONE FOR TODAY." : "";

            Draw.TextFixed(
                        this,
                        "PnL",
                        $"PnL: {todaysPnL:C2}{additionalText}",
                        TextPosition.BottomRight,
                        todaysPnL >= 0 ? Brushes.Green : Brushes.Red,            // Text color
                        new SimpleFont("Arial", 12), // Font and size
                        todaysPnL >= 0 ? Brushes.Green : Brushes.Red,      // Background color
                        Brushes.Transparent,      // Outline color
                        0                         // Opacity (0 is fully transparent)
                    );

            return reachDayLimit;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 100)
            {
                LocalPrint($"Not ENOUGH bars");
                return;
            }

            if (!IsTradingHour())
            {
                return;
            }

            if (ReachMaxDayLossOrDayTarget())
            {
                return;
            }

            if (DoubleBBStatus == ChickenStatus.OrderExists)
            {
                var existingOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                if (!existingOrders.Any())
                {
                    DoubleBBStatus = ChickenStatus.Idle;
                    LocalPrint($"ERROR: DoubleBBStatus không đúng. Reset status. - Current status: {DoubleBBStatus}");
                }
            }

            LocalPrint($"I'm still running. Current status: {DoubleBBStatus}- BarsInProgress: {BarsInProgress}");

            if (BarsInProgress == 0) // Current Frame
            {
                // Do nothing
            }
            else if (BarsInProgress == 2) //1 minute
            {
                // Cập nhật EMA29 và EMA51				
                ema29 = EMA(29).Value[0];
                ema51 = EMA(51).Value[0];
                LocalPrint($"EMA29: {ema29:F2}, EMA51: {ema51:F2}");
                /*
				* Nếu đang có lệnh chờ fill thì cập nhật lại lệnh 
				*/
                if (DoubleBBStatus == ChickenStatus.PendingFill)
                {
                    UpdatePendingOrder();
                }
                else if (DoubleBBStatus == ChickenStatus.OrderExists)
                {
                    // Move stop gain, stop loss
                }
            }
            else if (BarsInProgress == 1) // 5 minute
            {
                var bollinger = Bollinger(1, 20);

                upper = bollinger.Upper[0];
                lower = bollinger.Lower[0];
                middle = bollinger.Middle[0];

                low5m = Low[0];
                high5m = High[0];

                //Open and Close price of 5 minute frame

                var lastOpen = Open[1];
                var lastClose = Close[1];

                var currentOpen = Open[0];
                var currentPrice = Close[0];

                var currentDEMA = DEMA(DEMA_Period).Value[0];
                var last__DEMA = DEMA(DEMA_Period).Value[1];

                if (DoubleBBStatus == ChickenStatus.Idle) // Nếu đang có lệnh (OrderExist hoặc Pending) thi khong lam gi
                {
                    if (ShouldTrade(OrderAction.Sell, upper, lower, middle, currentPrice, currentOpen, lastOpen, lastClose, currentDEMA, last__DEMA))
                    {
                        EnterOrder(OrderAction.Sell, State, currentPrice, upper, lower, ema29, ema51);

                        Draw.ArrowDown(this, $"SellSignal" + CurrentBar, false, 0, High[0] + TickSize * 10, Brushes.Red);
                    }
                    else if (ShouldTrade(OrderAction.Buy, upper, lower, middle, currentPrice, currentOpen, lastOpen, lastClose, currentDEMA, last__DEMA))
                    {
                        EnterOrder(OrderAction.Buy, State, currentPrice, upper, lower, ema29, ema51);

                        Draw.ArrowUp(this, $"BuySignal" + CurrentBar, false, 0, Low[0] - TickSize * 10, Brushes.Green);
                    }
                }
                else if (DoubleBBStatus == ChickenStatus.PendingFill)
                {
                    UpdatePendingOrder();
                }
                else if (DoubleBBStatus == ChickenStatus.OrderExists)
                {
                    // Move stop gain, stop loss
                }
            }
        }

        // Trong qúa trình chờ lệnh được fill, có thể hết giờ hoặc chờ quá lâu
        void UpdatePendingOrder()
        {
            if (DoubleBBStatus != ChickenStatus.PendingFill)
            {
                return;
            }

            var existingOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.OrderState == OrderState.Accepted);
            if (!existingOrders.Any())
            {
                DoubleBBStatus = ChickenStatus.Idle;
                LocalPrint($"ERROR: DoubleBBStatus không đúng. Reset status. - Current status: {DoubleBBStatus}");
                return;
            }
            else if (existingOrders.Count() == 1 && existingOrders.First().Name.Contains("Entry")) //Really pending order
            {
                // Current status
                if (string.IsNullOrEmpty(atmStrategyId)) // We don't have any information related to atmStrategyId
                {
                    return;
                }

                var isMarketClosed = ToTime(Time[0]) >= 150000;

                var pendingOrder = existingOrders.FirstOrDefault();

                var cancelCausedByPrice = (pendingOrder.IsLong && high5m > upper) || (pendingOrder.IsShort && low5m < lower);

                var timeMoreThan60min = (Time[0] - pendingOrder.Time).TotalMinutes > 60;

                if (isMarketClosed || cancelCausedByPrice || timeMoreThan60min)
                {
                    AtmStrategyCancelEntryOrder(orderId);
                    orderId = null;
                    atmStrategyId = null;
                    DoubleBBStatus = ChickenStatus.Idle;
                    if (isMarketClosed)
                    {
                        LocalPrint($"Set IDLE và cancel lệnh do hết giờ trade.");
                    }
                    else if (cancelCausedByPrice)
                    {
                        LocalPrint($"Set IDLE và cancel lệnh do giá quá cao/thấp.");
                    }
                    else if (timeMoreThan60min)
                    {
                        LocalPrint($"Set IDLE và cancel lệnh do đợi quá lâu.");
                    }
                }
                else
                {
                    var newPrice = GetSetPrice(upper, lower, ema29, ema51, WayToTrade, pendingOrder.OrderAction);

                    LocalPrint($"Đang chờ, giá old Price {pendingOrder.LimitPrice:F2}, new Price: {newPrice:F2}");

                    LocalPrint($"Đã cập nhật lại giá entry - New price: ${newPrice:F2}");
                    filledPrice = newPrice;
                    AtmStrategyChangeEntryOrder(newPrice, 0, orderId);
                }
            }
            else if (!existingOrders.Any(ordr => ordr.Name.Contains("Entry"))) // Nếu có lệnh nhưng không phải là entry
            {
                DoubleBBStatus = ChickenStatus.OrderExists;
            }
            /*
			End of UpdatePendingOrder
			*/
        }

        /*
		This should be blank to easy to see the function
		*/
    }
}
