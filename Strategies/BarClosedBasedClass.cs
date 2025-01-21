#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Custom.Strategies;
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class BarClosedBasedClass : Strategy
	{
        private const int DEMA_Period = 9;

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
        
        protected double volume_5m = -1;
        #endregion 
        
        protected OrderAction? currentAction = null;
        protected ChickenStatus DoubleBBStatus = ChickenStatus.Idle;
        private ChickenStatus LastBBStatus_1m = ChickenStatus.Idle;
        protected double filledPrice = -1;

        #region Importants Configurations

        [NinjaScriptProperty]
        [Display(Name = "How to set stoploss/gain?", 
            Order = 2, 
            GroupName = "Importants Configurations")]
        public LossGainStrategy WayToSetStop { get; set; } = LossGainStrategy.BasedOnBollinger;


        /// <summary>
        /// Điểm vào lệnh (Theo EMA29/51 hay Bollinger band)
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enter order price:",
            Order = 3,
            GroupName = "Importants Configurations")]
        public ChickenWayToTrade WayToTrade { get; set; } = ChickenWayToTrade.EMA2951;

        /// <summary>
        /// ATM name for live trade. 
        /// </summary>
        [NinjaScriptProperty]
        [TypeConverter(typeof(ATMStrategyConverter))]
        [Display(Name = "ATM Strategy",
            Order = 4,
            GroupName = "Importants Configurations")]
        public string FullATMName { get; set; } = "Default_MNQ";

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy",
            Description = "Strategy sử dụng khi loss/gain more than a half of daily gain/loss",
            Order = 5,
            GroupName = "Importants Configurations")]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfATMName { get; set; } = "Half_MNQ";
        #endregion

        #region Parameters
        /// <summary>
        /// If loss is more than [MaximumDayLoss], stop trading for that day
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Maximum Day Loss ($)",
            Order = 5,
            GroupName = "Parameters")]
        public int MaximumDayLoss { get; set; } = 400;

        /// <summary>
        /// If gain is more than [StopWhenGain], stop trading for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop Trading if Profit is ($)",
            Order = 6,
            GroupName = "Parameters")]
        public int StopGainProfit { get; set; } = 700;

        /// <summary>
        /// Cho phép dịch chuyển stop loss và target
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Allow to move stop loss/profit target",
            Order = 8,
            GroupName = "Parameters")]
        public bool AllowToMoveStopLossGain { get; set; } = true;

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop loss (Ticks):", Order = 8, GroupName = "Parameters")]
        public int StopLossInTicks { get; set; } = 100; // 25 points for MNQ

        /// <summary>
        /// Thời gian có news, dừng trade trước và sau thời gian có news 5 phút
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
        public string NewsTimeInput { get; set; } = "0830";
        #endregion

        private List<int> NewsTimes = new List<int>();

        private readonly string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategy.txt");

        private double PointToMoveGainLoss = 5;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Play on 5 minutes frame.";
                Name = this.Name;
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 2; // Multiple 
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
                SetOrderQuantity = SetOrderQuantity.Strategy;
                DefaultQuantity = 2;

                // Set Properties
                FullATMName = "Half_MNQ";
                HalfATMName = "Half_MNQ";
                WayToTrade = ChickenWayToTrade.BollingerBand;

                MaximumDayLoss = 400;
                StopGainProfit = 700;
                AllowToMoveStopLossGain = true;
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

                PointToMoveGainLoss = 5;
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

        protected virtual bool ShouldTrade(OrderAction action)
        {
            /*
			* Điều kiện để trade (SHORT) 
			* 1. currentDEMA < upper & lastDEMA >= upper
			* 2. currentPrice > lower && currentPrice < upper
			*/
            var time = ToTime(Time[0]);

            // Cho phép trade reverse (Bollinger Band) từ 8:35 am đến 3:30pm
            if (time >= 083500 && time <= 233000) 
            {
                if (currentPrice > lowerBB_5m && currentPrice < upperBB_5m)
                {
                    if (action == OrderAction.Sell)
                    {
                        return lastDEMA_5m > upperBB_5m && currentDEMA_5m <= upperBB_5m; //&& lastOpen > upper;
                    }
                    else if (action == OrderAction.Buy)
                    {
                        return lastDEMA_5m < lowerBB_5m && currentDEMA_5m >= lowerBB_5m; //&& lastOpen < lower;
                    }
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

        /// <summary>
        /// Tìm giá để set dựa theo EMA29/51 hoặc dựa theo Bollinger bands
        /// </summary>
        /// <param name="chooseWay">Lựa chọn theo EMA29/51 hay theo Bollinger Bands</param>
        /// <param name="orderAction">Mua hoặc bán</param>
        /// <returns></returns>
        protected virtual double GetSetPrice(ChickenWayToTrade chooseWay, OrderAction orderAction)
        {
            double price = -1;
            var middleEMA = (ema29_1m + ema51_1m) / 2.0;

            if (orderAction == OrderAction.Buy)
            {   
                price = chooseWay == ChickenWayToTrade.EMA2951 ? middleEMA : lowerBB_5m;
            }
            else if (orderAction == OrderAction.Sell)
            {
                price = chooseWay == ChickenWayToTrade.EMA2951 ? middleEMA : upperBB_5m;
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

        const string SignalEntry1 = "Entry-MiddleBB";
        const string SignalEntry2 = "Entry-FullBB";
        private void EnterOrderPure(OrderAction action, double priceToSet, int quantity)
        {
            if (action == OrderAction.Buy)
            {
                var stopLossPrice = Math.Max(lowerStd2BB_5m, priceToSet - StopLossInTicks * TickSize);

                if (middleBB_5m > priceToSet + 7) // Phải đảm bảo cho việc từ priceToSet lên middle BB đủ room để chạy
                {
                    EnterLongLimit(quantity * 2, priceToSet, SignalEntry1);
                    SetStopLoss(SignalEntry1, CalculationMode.Price, stopLossPrice, false);
                    SetProfitTarget(SignalEntry1, CalculationMode.Price, middleBB_5m);
                }

                EnterLongLimit(quantity, priceToSet, SignalEntry2);                
                SetStopLoss(SignalEntry2, CalculationMode.Price, stopLossPrice, false);                
                SetProfitTarget(SignalEntry2, CalculationMode.Price, upperBB_5m);

                LocalPrint($"Enter LONG for {quantity * 3} contracts at {priceToSet:F2}, stop loss: {stopLossPrice:F2}, gain1: {middleBB_5m:F2}, gain2: {upperBB_5m:F2}");
            }
            else if (action == OrderAction.Sell)
            {
                var stopLossPrice = Math.Min(upperStd2BB_5m, priceToSet + StopLossInTicks * TickSize);

                if (middleBB_5m < priceToSet - 7)
                {
                    EnterShortLimit(quantity * 2, priceToSet, SignalEntry1);
                    SetStopLoss(SignalEntry1, CalculationMode.Price, stopLossPrice, false);
                    SetProfitTarget(SignalEntry1, CalculationMode.Price, middleBB_5m);
                }                

                EnterShortLimit(quantity, priceToSet, SignalEntry2);
                SetStopLoss(SignalEntry2, CalculationMode.Price, stopLossPrice, false);                
                SetProfitTarget(SignalEntry2, CalculationMode.Price, lowerBB_5m);

                LocalPrint($"Enter SHORT for {quantity * 3} contracts at {priceToSet:F2}, stop loss: {stopLossPrice:F2}, gain1: {middleBB_5m:F2}, gain2: {lowerBB_5m:F2}");
            }
        }

        protected virtual void EnterOrder(OrderAction action)
        {
            double priceToSet = GetSetPrice(WayToTrade, action);

            // Set new status
            DoubleBBStatus = ChickenStatus.PendingFill;
            currentAction = action;

            if (State == State.Realtime && WayToSetStop == LossGainStrategy.ChooseATM)
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
                EnterOrderPure(action, priceToSet, DefaultQuantity);
            }

            CalculatePnL();
        }

        /// <summary>
        /// Hàm này dùng cho [BACK TEST DATA] only - Move stop loss khi đóng nến để xét % gain/loss của giải thuật
        /// </summary>
        private void MoveStopLossToBreakEvenForBackTest()
        {
            LocalPrint($"MoveStopLossToBreakEvenForBackTest:: {State} {WayToSetStop}");
            if (State == State.Realtime || WayToSetStop != LossGainStrategy.BasedOnBollinger) // Ở chế độ realtime thì remove stop gain/loss ở OnMarketData
            {
                return;
            }

            var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && (order.OrderType == OrderType.StopLimit || order.OrderType == OrderType.StopMarket)).ToList();
            LocalPrint($"MoveStopLossToBreakEvenForBackTest:: {stopOrders.Count}");

            if (stopOrders.Count() != 1)
            {
                return;
            }

            LocalPrint($"MoveStopLossToBreakEvenForBackTest:: ");

            var stopOrder = stopOrders.FirstOrDefault();

            var shouldMoveBreakEven = (currentAction == OrderAction.Buy && currentPrice > middleBB_5m && stopOrder.StopPrice < filledPrice)
                || (currentAction == OrderAction.Sell && currentPrice < middleBB_5m && stopOrder.StopPrice > filledPrice);

            if (shouldMoveBreakEven) 
            {
                //ChangeOrder(stopOrder, 0, 0, filledPrice);                
                MoveTargetOrStopOrder(filledPrice, stopOrder, WayToSetStop, false, currentAction.ToString());
            }
        }

        private void MoveTargetOrStopOrder(double newTargetPrice, Order target, LossGainStrategy lossGainStrategy, bool isGainStop, string buyOrSell)
        {
            if (lossGainStrategy == LossGainStrategy.ChooseATM)
            {
                AtmStrategyChangeStopTarget(
                    isGainStop ? newTargetPrice : 0,
                    isGainStop ? 0 : newTargetPrice,
                    target.Name,
                    atmStrategyId);
            }
            else if (lossGainStrategy == LossGainStrategy.BasedOnBollinger)
            {
                if (isGainStop)
                {
                    SetProfitTarget(SignalEntry2, CalculationMode.Price, newTargetPrice);
                }
                else
                {
                    SetStopLoss(SignalEntry2, CalculationMode.Price, newTargetPrice, false);
                }
            }
            var text = isGainStop ? "TARGET" : "LOSS"; 

            LocalPrint($"Dịch chuyển {text} đến {newTargetPrice} - {buyOrSell}");
        }

        // Kéo stop loss/gain
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100)
            { 
                return;
            }

            if (DoubleBBStatus == ChickenStatus.OrderExists) // Điều chỉnh stop gain/loss 
            {
                if (!AllowToMoveStopLossGain)
                {
                    LocalPrint("NOT allow to move stop loss/gain");
                    return;
                }                
                
                if (WayToSetStop == LossGainStrategy.ChooseATM && string.IsNullOrEmpty(atmStrategyId))
                {
                    LocalPrint("atmStrategyId is null");
                    return;
                }

                try
                {
                    var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && (order.OrderType == OrderType.StopLimit || order.OrderType == OrderType.StopMarket)).ToList();
                    var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.OrderType == OrderType.Limit).ToList();

                    if (targetOrders.Count != 1 || stopOrders.Count != 1)
                    {
                        
                        return;
                    }

                    var targetOrder = targetOrders.FirstOrDefault();
                    var stopOrder = stopOrders.FirstOrDefault();

                    if (currentAction == OrderAction.Buy)
                    {
                        // Dịch stop gain nếu giá quá gần target
                        if (updatedPrice + PointToMoveGainLoss > targetOrder.LimitPrice)
                        {
                            MoveTargetOrStopOrder(targetOrder.LimitPrice + PointToMoveGainLoss, targetOrder, WayToSetStop, true, "BUY");
                        }

                        // Dịch chuyển stop loss nếu giá quá xa stop loss
                        if (stopOrder.StopPrice > filledPrice && stopOrder.StopPrice + PointToMoveGainLoss < updatedPrice)
                        {
                            MoveTargetOrStopOrder(updatedPrice - PointToMoveGainLoss, stopOrder, WayToSetStop, false, "BUY");
                        }

                        // Dịch stop loss lên break even 
                        if (currentPrice > middleBB_5m && stopOrder.StopPrice < filledPrice)
                        {
                            MoveTargetOrStopOrder(filledPrice, stopOrder, WayToSetStop, false, "BUY");                            
                        }   
                    }
                    else if (currentAction == OrderAction.Sell)
                    {
                        // Dịch stop gain nếu giá quá gần target
                        if (updatedPrice - PointToMoveGainLoss < targetOrder.LimitPrice)
                        {
                            MoveTargetOrStopOrder(targetOrder.LimitPrice - PointToMoveGainLoss, targetOrder, WayToSetStop, true, "SELL");
                        }

                        // Dịch chuyển stop loss nếu giá quá xa stop loss
                        if (stopOrder.StopPrice < filledPrice && stopOrder.StopPrice - PointToMoveGainLoss > updatedPrice)
                        {
                            MoveTargetOrStopOrder(updatedPrice + PointToMoveGainLoss, stopOrder, WayToSetStop, false, "SELL");
                        }

                        // Dịch stop loss lên break even 
                        if (currentPrice < middleBB_5m && stopOrder.StopPrice > filledPrice)
                        {                            
                            MoveTargetOrStopOrder(filledPrice, stopOrder, WayToSetStop, false, "SELL");
                        }
                    }
                }
                catch (Exception e)
                {
                    LocalPrint(e.Message);
                }
                          
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
            var time = ToTime(Time[0]);

            var newTime = NewsTimes.FirstOrDefault(c => NearNewsTime(time, c));

            if (newTime != 0)
            {
                LocalPrint($"News at {newTime} --> Not trading hour");
                return false;
            }

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

            var textColor = todaysPnL == 0 ? Brushes.Black : todaysPnL > 0 ? Brushes.Green : Brushes.Red;

            Draw.TextFixed(
                        this,
                        "PnL",
                        $"PnL: {todaysPnL:C2}{additionalText}",
                        TextPosition.BottomRight,
                        textColor,            // Text color
                        new SimpleFont("Arial", 12), // Font and size
                        textColor,      // Background color
                        Brushes.Transparent,      // Outline color
                        0                         // Opacity (0 is fully transparent)
                    );

            return reachDayLimit;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 30)
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

            LocalPrint($"Current status: {DoubleBBStatus}");

            if (DoubleBBStatus == ChickenStatus.OrderExists)
            {
                var existingOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

                if (!existingOrders.Any())
                {
                    DoubleBBStatus = ChickenStatus.Idle;
                    LocalPrint($"OnBarUpdate:: ERROR: DoubleBBStatus không đúng. Reset status. - Current status: {DoubleBBStatus}");
                }
            }

            if (BarsInProgress == 0) // Current Frame
            {
                // Do nothing
            }
            else if (BarsInProgress == 2) //1 minute
            {
                // Cập nhật EMA29 và EMA51	
                ema21_1m = EMA(21).Value[0];			
                ema29_1m = EMA(29).Value[0];
                ema51_1m = EMA(51).Value[0];
                ema120_1m = EMA(120).Value[0];

                currentPrice = Close[0];

                /*
				* Nếu đang có lệnh chờ fill thì cập nhật lại lệnh 
				* Do 2 events BarClosed ở khung 5 phút và 1 phút xuất hiện gần như đồng thời, 
				* nên cần chắc chắn là việc cập nhật PendingOrder chỉ xảy ra ở cây nến tiếp theo
				*/
                if (DoubleBBStatus == ChickenStatus.PendingFill)
                {
                    if (LastBBStatus_1m == ChickenStatus.PendingFill)
                    {
                        UpdatePendingOrder("1m");
                    }
                }
                else if (DoubleBBStatus == ChickenStatus.OrderExists)
                {
                    // Move to break even
                    MoveStopLossToBreakEvenForBackTest();
                }
                LastBBStatus_1m = DoubleBBStatus;
            }
            else if (BarsInProgress == 1) // 5 minute
            {
                var bollinger = Bollinger(1, 20);
                var bollingerStd2 = Bollinger(2, 20);

                volume_5m = Volume[0];

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

                if (DoubleBBStatus == ChickenStatus.Idle) // Nếu đang có lệnh (OrderExist hoặc Pending) thi khong lam gi
                {
                    if (ShouldTrade(OrderAction.Sell))
                    {
                        EnterOrder(OrderAction.Sell);

                        LocalPrint($"Enter Sell at {Time[0]}");

                        Draw.ArrowDown(this, $"SellSignal" + CurrentBar, false, 0, High[0] + TickSize * 10, Brushes.Red);
                    }
                    else if (ShouldTrade(OrderAction.Buy))
                    {
                        EnterOrder(OrderAction.Buy);

                        LocalPrint($"Enter Buy at {Time[0]}");

                        Draw.ArrowUp(this, $"BuySignal" + CurrentBar, false, 0, Low[0] - TickSize * 10, Brushes.Green);
                    }
                }
                else if (DoubleBBStatus == ChickenStatus.PendingFill)
                {
                    UpdatePendingOrder("5 mins");
                }
                else if (DoubleBBStatus == ChickenStatus.OrderExists)
                {                    
                    // Move to break even
                    MoveStopLossToBreakEvenForBackTest();
                }
            }
        }

        // Trong qúa trình chờ lệnh được fill, có thể hết giờ hoặc chờ quá lâu
        protected virtual void UpdatePendingOrder(string barInProgress = "")
        {
            if (DoubleBBStatus != ChickenStatus.PendingFill)
            {
                return;
            }            

            var existingOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

            var entriedOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && (order.Name == SignalEntry1 || order.Name == SignalEntry2));

            if (!existingOrders.Any())
            {
                DoubleBBStatus = ChickenStatus.Idle;                

                LocalPrint($"UpdatePendingOrder {barInProgress}:: ERROR: DoubleBBStatus không đúng. Reset status. - Current status: {DoubleBBStatus}");

                return;
            }
            else if (entriedOrders.Any()) // Entry by 
            {
                filledPrice = GetSetPrice(WayToTrade, currentAction.Value);                

                LocalPrint($"Set new price: {filledPrice:F2}");
                
                foreach (var order in entriedOrders)
                {
                    CancelOrder(order);                        
                }

                EnterOrderPure(currentAction.Value, filledPrice, DefaultQuantity);                                              
            }
            else if (existingOrders.Count() == 1 && existingOrders.First().Name.Contains("Entry")) //Really pending order (ATM order)
            {
                // Current status
                if (string.IsNullOrEmpty(atmStrategyId)) // We don't have any information related to atmStrategyId
                {
                    return;
                }

                var isMarketClosed = ToTime(Time[0]) >= 150000;

                var pendingOrder = existingOrders.FirstOrDefault();

                LocalPrint($"UpdatePendingOrder - open: {openPrice_5m}, close: {closePrice_5m}, pendingOrder.IsLong: {pendingOrder.IsLong}, high5m: {highPrice_5m}, upper: {upperBB_5m}, upperStd2: {upperStd2BB_5m},  || pendingOrder.IsShort: {pendingOrder.IsShort}, low5m: {lowPrice_5m},  lower: {lowerBB_5m}");

                var cancelCausedByPrice = (pendingOrder.IsLong && highPrice_5m > upperBB_5m) || (pendingOrder.IsShort && lowPrice_5m < lowerBB_5m);

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
                    filledPrice = GetSetPrice(WayToTrade, pendingOrder.OrderAction);

                    LocalPrint($"Đang chờ, giá old Price {pendingOrder.LimitPrice:F2}, new Price: {filledPrice:F2}");

                    LocalPrint($"Đã cập nhật lại giá entry - New price: ${filledPrice:F2}");                    
                    
                    AtmStrategyChangeEntryOrder(filledPrice, 0, orderId);
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
