//#define ENABLE_ADX_DI

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
using System.Windows;
using System.Threading.Tasks;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public class BarClosedBasedClass : Strategy
    {
        private const int DEMA_Period = 9;
        private const int FiveMinutes_Period = 14;
        private const int ADX_Min_Level = 25; 

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

        protected double lastUpperBB_5m = -1;
        protected double lastLowerBB_5m = -1;

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double currentDEMA_5m = -1;
        protected double lastDEMA_5m = -1;

        protected int barIndex_5m = 0;

        // Volume 
        protected double volume_5m = -1;
        protected double avgEMAVolume_5m = -1;
        protected double volumeBuy_5m = -1;
        protected double volumeSell_5m = -1;
        // ADX
        protected double adx_5m = -1;
        protected double plusDI_5m = -1;
        protected double minusDI_5m = -1;

        // WAE Values 
        protected double waeDeadVal_5m = -1;
        protected double waeExplosion_5m = -1;
        protected double waeUptrend_5m = -1;
        protected double waeDowntrend_5m = -1;
        
        private Series<double> deadZoneSeries;
        #endregion

        protected TradeAction currentTradeAction = TradeAction.NoTrade;

        /// <summary>
        /// Biến này dùng để di chuyển stop loss khi giá BẮT ĐẦU gần chạm đến target2 (để room cho chạy).
        /// </summary>
        private bool startMovingStoploss = false;

        /// <summary>
        /// Lệnh hiện tại là lệnh mua
        /// </summary>
        private bool IsBuying
        {
            get
            {
                return currentTradeAction == TradeAction.Buy_Reversal || currentTradeAction == TradeAction.Buy_Trending;
            }
        }

        /// <summary>
        /// Lệnh hiện tại là lệnh bán 
        /// </summary>
        private bool IsSelling
        {
            get
            {
                return currentTradeAction == TradeAction.Sell_Reversal || currentTradeAction == TradeAction.Sell_Trending;
            }
        }

        private bool IsReverseTrade
        {
            get
            {
                return currentTradeAction == TradeAction.Sell_Reversal || currentTradeAction == TradeAction.Buy_Reversal;
            }
        }

        private bool IsTrendingTrade
        {
            get
            {
                return currentTradeAction == TradeAction.Buy_Trending || currentTradeAction == TradeAction.Sell_Trending;
            }
        }

        protected Trends CurrentTrend = Trends.Unknown;

        /// <summary>
        /// Giá fill lệnh ban đầu 
        /// </summary>
        protected double filledPrice = -1;

        protected DateTime filledTime = DateTime.Now;
        private ChickenStatus ChickenStatus
        {
            get 
            {
                if (!ActiveOrders.Any())
                {
                    return ChickenStatus.Idle;
                }
                else if (ActiveOrders.Values.Any(order => SignalEntries.Contains(order.Name)))
                {
                    return ChickenStatus.PendingFill;
                }
                
                return ChickenStatus.OrderExists;                
            }
        }

        /// <summary>
        /// Realtime: Dùng order.Id làm key, không phải Realtime: Dùng Name làm key
        /// </summary>
        private Dictionary<string,Order> ActiveOrders = new Dictionary<string, Order>();                

        #region Importants Configurations

        /// <summary>
        /// Điểm vào lệnh (Theo EMA29/51 hay Bollinger band)
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enter order price:",
            Order = 3,
            GroupName = "Importants Configurations")]
        public ChickenWayToTrade WayToTrade { get; set; } = ChickenWayToTrade.EMA2951;
        #endregion

        #region Parameters
        /// <summary>
        /// If loss is more than [MaximumDayLoss], stop trading for that day
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Maximum Day Loss ($)",
            Order = 5,
            GroupName = "Parameters")]
        public int MaximumDailyLoss { get; set; } = 400;

        /// <summary>
        /// If gain is more than [StopWhenGain], stop trading for that day 
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop Trading if daily Profit is ($)",
            Order = 6,
            GroupName = "Parameters")]
        public int DailyTargetProfit { get; set; } = 700;

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
        [Display(Name = "Stop loss (Ticks):", 
            Order = 8, 
            GroupName = "Parameters")]
        public int StopLossInTicks { get; set; } = 120; // 25 points for MNQ

        /// <summary>
        /// Thời gian có news, dừng trade trước và sau thời gian có news 5 phút
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
        public string NewsTimeInput { get; set; } = "0830,0500";
        #endregion

        private List<int> NewsTimes = new List<int>();

        private double PointToMoveGainLoss = 7;

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
                BarsRequiredToTrade = 30;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
                SetOrderQuantity = SetOrderQuantity.Strategy;
                DefaultQuantity = 2;

                // Set Properties

                WayToTrade = ChickenWayToTrade.EMA2951;

                MaximumDailyLoss = 400;
                DailyTargetProfit = 700;
                AllowToMoveStopLossGain = true;
                NewsTimeInput = "0830,0500";
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
                    Print($"ERROR: " + e.Message);
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

                deadZoneSeries = new Series<double>(this);
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
                    LocalPrint("ERROR" + e.Message);
                }
            }
        }

        protected virtual TradeAction ShouldTrade()
        {
            /*
			* Điều kiện để trade (SHORT) 
			* 1. currentDEMA < upper & lastDEMA >= upper
			* 2. currentPrice > lower && currentPrice < upper
			*/
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return TradeAction.NoTrade;
            }

            // Cho phép trade reverse (Bollinger Band) từ 8:35 am đến 11:30pm
            if (currentPrice > lowerBB_5m && currentPrice < upperBB_5m)
            {
                LocalPrint($"**********Kiểm tra điều kiện trade Reversal***************");
                if (lastDEMA_5m > lastUpperBB_5m && currentDEMA_5m <= upperBB_5m)
                {
                    LocalPrint("Found SELL signal (Reversal)");

                    filledTime = Time[0];

                    return TradeAction.Sell_Reversal;
                }
                else if (lastDEMA_5m < lastLowerBB_5m && currentDEMA_5m >= lowerBB_5m)
                {
                    LocalPrint("Found BUY signal (Reversal)");

                    filledTime = Time[0];

                    return TradeAction.Buy_Reversal;
                }
            }

            // Trade theo trending
            if (waeExplosion_5m < waeDowntrend_5m && waeDeadVal_5m < waeDowntrend_5m /* && waeDeadVal_5m < waeExplosion_5m*/ )
            {
                LocalPrint($"Found SELL signal (Trending) - waeDeadVal_5m: {waeDeadVal_5m:N2}, waeExplosion_5m: {waeExplosion_5m:N2}, waeDowntrend_5m: {waeDowntrend_5m:N2}");

                filledTime = Time[0];

                return TradeAction.Sell_Trending;
            }
            else if (waeExplosion_5m < waeUptrend_5m && waeDeadVal_5m < waeUptrend_5m /*waeDeadVal_5m < waeExplosion_5m && */ )
            {
                LocalPrint($"Found BUY signal (Trending) - waeDeadVal_5m: {waeDeadVal_5m:N2}, waeExplosion_5m: {waeExplosion_5m:N2}, waeUptrend_5m: {waeUptrend_5m:N2}");

                filledTime = Time[0];

                return TradeAction.Buy_Trending;
            }            

            return TradeAction.NoTrade;
        }        

        /// <summary>
        /// Tìm giá để set dựa theo EMA29/51 hoặc dựa theo Bollinger bands
        /// </summary>        
        /// <param name="tradeAction">NoTrade, Sell_Reversal, Buy_Reversal, Sell_Trending, Buy_Trending</param>
        /// <returns></returns>
        protected virtual double GetSetPrice(TradeAction tradeAction)
        {
            double price = -1;
            var middleEMA = (ema29_1m + ema51_1m) / 2.0;

            switch (tradeAction)
            { 
                case TradeAction.Buy_Trending:
                case TradeAction.Sell_Trending:
                    price = middleEMA;
                    break; 

                case TradeAction.Sell_Reversal: 
                    price = WayToTrade == ChickenWayToTrade.EMA2951 ? middleEMA : upperBB_5m;
                    break;

                case TradeAction.Buy_Reversal:
                    price = WayToTrade == ChickenWayToTrade.EMA2951 ? middleEMA : lowerBB_5m;
                    break;
            }            

            return Math.Round(price * 4, MidpointRounding.AwayFromZero) / 4.0;
        }        
        protected virtual double GetStopLossPrice(TradeAction tradeAction, double setPrice)
        {
            double price = -1;            

            switch (tradeAction)
            {
                case TradeAction.Buy_Trending:
                    price = setPrice - TickSize * StopLossInTicks;
                    break;

                case TradeAction.Sell_Trending:
                    price = setPrice + TickSize * StopLossInTicks;
                    break;

                case TradeAction.Buy_Reversal:
                    price = setPrice - TickSize * StopLossInTicks;
                    break;

                case TradeAction.Sell_Reversal:
                    price = setPrice + TickSize * StopLossInTicks;
                    break;
            }

            return Math.Round(price * 4, MidpointRounding.AwayFromZero) / 4.0;
        }
        private double GetTargetPrice_Half(TradeAction tradeAction, double setPrice)
        {
            double price = -1;

            var time = ToTime(Time[0]); 

            switch (tradeAction)
            {
                case TradeAction.Buy_Trending:
                    price = (time > 150000 || time < 083000) // if night time, cut half at 7.5 point
                        ? setPrice + 7.5
                        : setPrice + (TickSize * StopLossInTicks / 2);
                    break;

                case TradeAction.Sell_Trending:
                    price = (time > 150000 || time < 083000) // if night time, cut half at 7.5 point
                        ? setPrice - 7.5
                        : setPrice - (TickSize * StopLossInTicks /2);
                    break;

                case TradeAction.Buy_Reversal:
                    price = middleBB_5m;
                    break;

                case TradeAction.Sell_Reversal:
                    price = middleBB_5m;
                    break;
            }

            return Math.Round(price * 4, MidpointRounding.AwayFromZero) / 4.0;
        }
        private double GetTargetPrice_Two(TradeAction tradeAction, double setPrice)
        {
            double price = -1;

            switch (tradeAction)
            {
                case TradeAction.Buy_Trending:
                    price = setPrice + TickSize * StopLossInTicks;
                    break;

                case TradeAction.Sell_Trending:
                    price = setPrice - TickSize * StopLossInTicks;
                    break;

                case TradeAction.Buy_Reversal:
                    price = upperBB_5m;
                    break;

                case TradeAction.Sell_Reversal:
                    price = lowerBB_5m;
                    break;
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

        const string SignalEntry_ReversalHalf = "Entry-RH";
        const string SignalEntry_ReversalFull = "Entry-RF";
        const string SignalEntry_TrendingHalf = "Entry-TH";
        const string SignalEntry_TrendingFull = "Entry-TF";

        HashSet<string> SignalEntries = new HashSet<string>
        {
            SignalEntry_ReversalHalf,
            SignalEntry_ReversalFull,
            SignalEntry_TrendingHalf,
            SignalEntry_TrendingFull
        };
        
        private void EnterOrderPure(double priceToSet, double target, double stoploss, string signal, int quantity = 2)
        {
            var text = IsBuying ? "LONG" : "SHORT";
            
            var allowTrade = (IsBuying && priceToSet < target) || (IsSelling && priceToSet > target);

            if (allowTrade)
            {
                if (IsBuying)
                {
                    EnterLongLimit(2, true, quantity, priceToSet, signal);
                }                   
                else
                {
                    EnterShortLimit(2, true, quantity, priceToSet, signal);
                }

                SetStopLoss(signal, CalculationMode.Price, stoploss, false);
                SetProfitTarget(signal, CalculationMode.Price, target);

                LocalPrint($"Enter {text}  for {quantity} contracts with signal {signal} at {priceToSet:N2}, stop loss: {stoploss:N2}, target: {target:N2}");
            }
        }        

        protected virtual void EnterOrder(TradeAction tradeAction)
        {
            // Set global values
            currentTradeAction = tradeAction;

            // Chưa cho move stop loss
            startMovingStoploss = false;

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            LocalPrint($"Enter {action} at {Time[0]}");

            if (action == OrderAction.Buy)
            {
                Draw.ArrowUp(this, $"BuySignal" + barIndex_5m, false, 0, lowPrice_5m - TickSize * 10, Brushes.Green);
            }
            else if (action == OrderAction.Sell)
            {
                Draw.ArrowDown(this, $"SellSignal" + barIndex_5m, false, 0, highPrice_5m + TickSize * 10, Brushes.Red);
            }            

            double priceToSet = GetSetPrice(tradeAction);
            filledPrice = priceToSet;
            
            var stopLossPrice = GetStopLossPrice(currentTradeAction, priceToSet); 
            var targetHalf = GetTargetPrice_Half(currentTradeAction, priceToSet);
            var targetFull = GetTargetPrice_Two(currentTradeAction, priceToSet);
            
            try
            {               
                var signalHalf = IsTrendingTrade ? SignalEntry_TrendingHalf : SignalEntry_ReversalHalf;
                EnterOrderPure(priceToSet, targetHalf, stopLossPrice, signalHalf, DefaultQuantity);
                
                var signalFull = IsTrendingTrade ? SignalEntry_TrendingFull : SignalEntry_ReversalFull;
                EnterOrderPure(priceToSet, targetFull, stopLossPrice, signalFull, DefaultQuantity);
            }
            catch (Exception ex) 
            {
                LocalPrint($"ERROR: " + ex.Message);
            }
        }

        /// <summary>
        /// Move half price target dựa trên giá Bollinger Band Middle
        /// </summary>
        private void MoveTarget1BasedOnBollinger()
        {
            var targetHalfPriceOrders = ActiveOrders.Values.Where(c => c.FromEntrySignal == SignalEntry_ReversalHalf && 
                (c.OrderType == OrderType.StopMarket || c.OrderType == OrderType.StopLimit )).ToList();

            foreach (var order in targetHalfPriceOrders)
            {
                if ((IsBuying && middleBB_5m > filledPrice) || (IsSelling && middleBB_5m < filledPrice))
                {
                    MoveTargetOrStopOrder(middleBB_5m, order, true, IsBuying ? "BUY" : "SELL", order.FromEntrySignal);
                }                
            }
        }
        /// <summary>
        /// Dịch chuyển 1 stop loss hoặc target order
        /// </summary>
        /// <param name="newPrice">Giá mới cần chuyển đến</param>
        /// <param name="order">Order</param>
        /// <param name="isGainStop">isGainStop = true: Profit order, isGainStop = false : Profit order</param>
        /// <param name="buyOrSell">Lệnh này là bán hay mua (dùng cho logger nên không quá quan trọng)</param>
        /// <param name="fromEntrySignal">Entry Signal</param>
        private void MoveTargetOrStopOrder(double newPrice, Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
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

            LocalPrint($"Dịch chuyển order {order.Name}, id: {order.Id}({text}), {order.Quantity} contract(s) từ [{(isGainStop ? order.LimitPrice  : order.StopPrice)}] đến [{newPrice}] - {buyOrSell}");
        }

        protected virtual void MoveStopOrder(Order stopOrder, double updatedPrice)
        {
            LocalPrint($"Trying to move stop order. Filled Price: [{filledPrice:N2}], current Stop: {stopOrder.StopPrice}, updatedPrice: [{updatedPrice}]");
            double newPrice = -1;
            var allowMoving = "";
           
            // Dịch stop loss lên break even 
            if (IsBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (startMovingStoploss && stopOrder.StopPrice > filledPrice && stopOrder.StopPrice + PointToMoveGainLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveGainLoss;
                    allowMoving = "BUY";
                }
                // Kéo về break even
                else if (stopOrder.StopPrice < filledPrice && filledPrice + 1 < updatedPrice)
                {
                    newPrice = filledPrice + 1;
                    allowMoving = "BUY";
                }
            }
            else if (IsSelling)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (startMovingStoploss &&  stopOrder.StopPrice < filledPrice && stopOrder.StopPrice - PointToMoveGainLoss > updatedPrice)
                {
                    newPrice = updatedPrice + PointToMoveGainLoss;
                    allowMoving = "SELL";
                }
                // Kéo về break even
                else if (stopOrder.StopPrice > filledPrice && filledPrice - 1 > updatedPrice)
                {
                    newPrice = filledPrice - 1;
                    allowMoving = "SELL";
                }
            }

            if (allowMoving != "")
            {                
                MoveTargetOrStopOrder(newPrice, stopOrder, false, allowMoving, stopOrder.FromEntrySignal);
            }
        }

        protected virtual void MoveTargetOrder(Order targetOrder, double updatedPrice)
        {
            // Dịch stop gain nếu giá quá gần target            
            if (IsBuying && updatedPrice + PointToMoveGainLoss > targetOrder.LimitPrice)
            {
                MoveTargetOrStopOrder(targetOrder.LimitPrice + PointToMoveGainLoss, targetOrder, true, "BUY", targetOrder.FromEntrySignal);

                startMovingStoploss = true;
            }
            else if (IsSelling && updatedPrice - PointToMoveGainLoss < targetOrder.LimitPrice)
            {
                MoveTargetOrStopOrder(targetOrder.LimitPrice - PointToMoveGainLoss, targetOrder, true, "SELL", targetOrder.FromEntrySignal);

                startMovingStoploss = true;
            }
            
        }

        // Kéo stop loss/gain
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;            

            if (updatedPrice < 100)
            {
                return;
            }

            if (ChickenStatus == ChickenStatus.OrderExists)
            {
                if (!AllowToMoveStopLossGain)
                {
                    LocalPrint("NOT allow to move stop loss/gain");
                    return;
                }
                
                try
                {
                    // Order với half price
                    var hasHalfPriceOder = ActiveOrders.Values.Any(order => order.FromEntrySignal == SignalEntry_ReversalHalf || order.FromEntrySignal == SignalEntry_TrendingHalf);

                    if (hasHalfPriceOder) // Nếu còn order với half price (Chưa cắt half) --> Không nên làm gì
                    {
                        return;
                    }

                    var stopOrders = ActiveOrders.Values.Where(order => order.OrderType == OrderType.StopMarket || order.OrderType == OrderType.StopLimit)
                        .ToList();

                    LocalPrint($"StopLoss Order Count: {stopOrders.Count}, all active orders count: {ActiveOrders.Count}");

                    var targetOrders = ActiveOrders.Values.Where(order => order.OrderState == OrderState.Working && order.OrderType == OrderType.Limit)
                        .ToList();

                    for (var i =0; i< stopOrders.Count; i++)
                    {
                        var stopOrder = stopOrders[i];
                        LocalPrint($"StopLoss Order ID: {stopOrder.Id} - Price: {stopOrder.StopPrice}");
                        MoveStopOrder(stopOrder, updatedPrice);
                    }

                    for (var i = 0; i < targetOrders.Count; i++)
                    {
                        var targetOrder = targetOrders[i];
                        MoveTargetOrder(targetOrder, updatedPrice);
                    }
                }
                catch (Exception e)
                {
                    LocalPrint($"ERROR: " + e.Message);
                }                
            }
        }

        private string GenerateKey(Order order)
        {
            // Order là Entry thì dùng Name làm Key
            if (SignalEntries.Any(signal => signal == order.Name))
            {
                return order.Name;
            }
            // Order không phải entry --> Name sẽ có dạng "Stop loss" hoặc "Profit target"
            else if (order.Name == StopLoss_SignalName || order.Name == ProfitTarget_SignalName)
            {
                // Back test data, không có Id
                if (order.Id == -1)
                {
                    return $"{order.Name}-{order.FromEntrySignal}";
                }
                else 
                {
                    return $"{order.Id}";
                } 
            }
            return $"{order.Name}-{order.FromEntrySignal}-{order.Id}";
        }

        const string StopLoss_SignalName = "Stop loss";
        const string ProfitTarget_SignalName = "Profit target";

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
            var key = GenerateKey(order);            

            try
            {
                /*
                LocalPrint(
                $"OnOrderUpdate - key: {key}, orderType: {order.OrderType}, orderState: {orderState}, " +
                $"limitPrice: {limitPrice:N2}, stop: {stopPrice:N2}");
                */
                if (orderState == OrderState.Filled || orderState == OrderState.Cancelled)
                {
                    ActiveOrders.Remove(key);
                }
                else if (orderState == OrderState.Working || orderState == OrderState.Accepted)
                {
                    // Add or update 
                    ActiveOrders[key] = order;
                }

                
            }
            catch (Exception e)
            {
                LocalPrint("ERROR: ********" + e.Message + "************");
            }
            finally 
            {
                //LocalPrint($"CountOrders: {ActiveOrders.Count}");
                LocalPrint(
                    $"[OnOrderUpdate] - key: [{key}], quantity: {quantity}, filled: {filled}, orderType: {order.OrderType}, orderState: {orderState}, " +
                    $"limitPrice: {limitPrice:N2}, stop: {stopPrice:N2}. Current number of active orders: {ActiveOrders.Count}");
            }
        }

        

        // Check if current time is still from 8:05:10 AM to 3:00 PM.
        private bool IsTradingHour()
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

        /// <summary>
        /// Hàm này sử dụng cho khung 1 phút
        /// </summary>
        private void DrawImportantLevels()
        {
            // EMA 29/51
            var middleEMA2951 = (ema29_1m + ema51_1m) / 2;
            Draw.HorizontalLine(this, "MiddleEMA", middleEMA2951, Brushes.Gold, Gui.DashStyleHelper.Dot, 2);
            Draw.Text(this, "MiddleEMA_Label", true, $"[{middleEMA2951:N2}]",
                -3,
                middleEMA2951,
                5,
                Brushes.Green,
                new SimpleFont("Arial", 10),
                TextAlignment.Left,
                Brushes.Transparent,
                Brushes.Transparent, 0);
        }
        
        protected override void OnBarUpdate()
        {            
            if (CurrentBar < BarsRequiredToTrade)
            {
                //LocalPrint($"Not ENOUGH bars");
                return;
            }

            if (!IsTradingHour())
            {
                if (ChickenStatus == ChickenStatus.Idle)
                {
                    return;
                }
                else if (ChickenStatus == ChickenStatus.PendingFill)
                {
                    LocalPrint($"Gần giờ có news, cancel những lệnh chờ đang có");
                    CancelAllPendingOrder();
                    return;
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

            if (StrategiesUtilities.ReachMaxDayLossOrDayTarget(this, Account, MaximumDailyLoss, DailyTargetProfit))
            {                
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                // Cập nhật EMA29 và EMA51	
                ema21_1m = EMA(21).Value[0];
                ema29_1m = EMA(29).Value[0];
                ema51_1m = EMA(51).Value[0];
                ema120_1m = EMA(120).Value[0];

                currentPrice = Close[0];

                DrawImportantLevels();
                
                if (ChickenStatus == ChickenStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if (shouldTrade != TradeAction.NoTrade)
                    {
                        EnterOrder(shouldTrade);
                    }
                }
                else if (ChickenStatus == ChickenStatus.PendingFill)
                {
                    UpdatePendingOrder();
                }
                else if (ChickenStatus == ChickenStatus.OrderExists)
                {
                    MoveTarget1BasedOnBollinger();
                }
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                if (BarsInProgress == 0)
                {
                    // Current View --> return
                    return;
                }
                var bollinger = Bollinger(1, 20);
                var bollingerStd2 = Bollinger(2, 20);

                volume_5m = Volume[0];
                avgEMAVolume_5m = EMA(Volume, FiveMinutes_Period)[0];
                adx_5m = ADX(FiveMinutes_Period).Value[0];                
                
                plusDI_5m = DM(FiveMinutes_Period).DiPlus[0];
                minusDI_5m = DM(FiveMinutes_Period).DiMinus[0];

                upperBB_5m = bollinger.Upper[0];
                lowerBB_5m = bollinger.Lower[0];
                middleBB_5m = bollinger.Middle[0];

                lastUpperBB_5m = bollinger.Upper[1];
                lastLowerBB_5m = bollinger.Lower[1];

                upperStd2BB_5m = bollingerStd2.Upper[0];
                lowerStd2BB_5m = bollingerStd2.Lower[0];

                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                barIndex_5m = CurrentBar;

                currentDEMA_5m = DEMA(DEMA_Period).Value[0];
                lastDEMA_5m = DEMA(DEMA_Period).Value[1];
                currentPrice = Close[0];

                var wae = FindWaddahAttarExplosion();

                waeDeadVal_5m = wae.DeadZoneVal; 
                waeDowntrend_5m = wae.DownTrendVal;
                waeExplosion_5m = wae.ExplosionVal;
                waeUptrend_5m = wae.UpTrendVal;

                LocalPrint($"WAE Values: DeadZoneVal: {wae.DeadZoneVal:N2}, ExplosionVal: {wae.ExplosionVal:N2}, " +
                    $"DowntrendVal: {wae.DownTrendVal:N2}, " +
                    $"UptrendVal: {wae.UpTrendVal:N2}. ADX = {adx_5m:N2} " +
                    $"{(wae.CanTrade ? "--> Can enter Order" : "")}");
            }
        }
        /// <summary>
        /// Tìm các giá trị của Waddah Attar Explosion ở khung 5 phút
        /// </summary>
        /// <returns></returns>
        private WEA_ValueSet FindWaddahAttarExplosion()
        {
            int sensitivity = 150;
            int fastLength = 20;
            int slowLength = 40;
            int channelLength = 20;
            double mult = 2.0;

            // WAE
            // Calculate Typical Price
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3.0;

            // Calculate True Range and store it in a Series
            double trueRange = Math.Max(High[0] - Low[0], Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
            deadZoneSeries[0] = trueRange; // Initialize the first value

            // Calculate smoothed ATR using EMA of the True Range Series
            double smoothedATR = EMA(deadZoneSeries, 100)[0];

            // Dead Zone
            double deadZone = smoothedATR * 3.7;

            // MACD Difference Calculation
            double fastEMA = EMA(Close, fastLength)[0];
            double slowEMA = EMA(Close, slowLength)[0];
            double prevFastEMA = EMA(Close, fastLength)[1];
            double prevSlowEMA = EMA(Close, slowLength)[1];

            double macd = fastEMA - slowEMA;
            double prevMacd = prevFastEMA - prevSlowEMA;
            double trendCalculation = (macd - prevMacd) * sensitivity;

            // Bollinger Bands Calculation
            double bbBasis = SMA(Close, channelLength)[0];
            double bbDev = mult * StdDev(Close, channelLength)[0];
            double bbUpperVal = bbBasis + bbDev;
            double bbLowerVal = bbBasis - bbDev;

            // Explosion Line
            double explosionValue = bbUpperVal - bbLowerVal;            

            return new WEA_ValueSet
            {
                DeadZoneVal = deadZone, 
                DownTrendVal = trendCalculation < 0 ? -trendCalculation : 0,
                ExplosionVal = explosionValue,
                UpTrendVal = trendCalculation >= 0 ? trendCalculation : 0
            };
        }

        private void CancelAllPendingOrder()
        {
            var clonedList = ActiveOrders.Values.ToList();
            foreach (var order in clonedList)
            { 
                CancelOrder(order);
            }
        }

        /// <summary>
        /// Khi State từ Historical sang Realtime thì trong ActiveOrders có thể còn lệnh
        /// Nếu ChickenStatus == ChickenStatus.OrderExists thì các lệnh trong đó là các lệnh fake
        /// Nếu ChickenStatus == ChickenStatus.PendingFill thì phải transite các lệnh này sang chế độ LIVE
        /// </summary>
        private void TransitionOrdersToLive()
        {
            if (ChickenStatus == ChickenStatus.OrderExists)
            {
                LocalPrint($"Transition to live, clear all ActiveOrders");
                /*
                if (IsBuying)
                {
                    ExitLong();
                }
                else if (IsSelling)
                {
                    ExitShort();
                }
                */

                var clonedList = ActiveOrders.Values.Where(c => c.OrderType == OrderType.Limit).ToList();

                for (var i = 0; i < clonedList.Count; i++)
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
            else if (ChickenStatus == ChickenStatus.PendingFill)
            {
                LocalPrint($"Transition to live, convert all pending fill orders to realtime");
                var clonedList = ActiveOrders.Values.ToList();
                foreach (var order in clonedList)
                {
                    GetRealtimeOrder(order);
                }
            }            
        }

        private bool IsHalfPriceOrder(Order order)
        {
            return order.Name == SignalEntry_ReversalHalf || order.Name == SignalEntry_TrendingHalf;
        }
        private bool IsFullPriceOrder(Order order)
        {
            return order.Name == SignalEntry_ReversalFull || order.Name == SignalEntry_TrendingFull;
        }

        // Trong qúa trình chờ lệnh được fill, có thể hết giờ hoặc chờ quá lâu
        protected virtual void UpdatePendingOrder()
        {
            if (ChickenStatus != ChickenStatus.PendingFill)
            {
                return;
            }

            /*
             * Kiểm tra điều kiện để cancel lệnh
             */

            // Cancel lệnh do đợi quá lâu
            var firstOrder = ActiveOrders.First().Value;
            if ((Time[0] - filledTime).TotalMinutes > 60)
            {
                //Account.CancelAllOrders(Instrument);
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh do đợi quá lâu");
                return; 
            }

            // Cancel lệnh hết giờ trade
            if (ToTime(Time[0]) >= 150000 && ToTime(firstOrder.Time) < 150000)
            {
                //Account.CancelAllOrders(Instrument);
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh hết giờ trade");
                return;
            }

            // Cancel cho lệnh theo đánh theo Bollinger (Ngược trend) 
            if (firstOrder.FromEntrySignal == SignalEntry_ReversalFull || firstOrder.FromEntrySignal == SignalEntry_ReversalHalf)
            {
                var cancelCausedByPrice = (firstOrder.IsLong && (highPrice_5m > upperStd2BB_5m || currentDEMA_5m > upperBB_5m)) 
                    || (firstOrder.IsShort && (lowPrice_5m < lowerStd2BB_5m || currentDEMA_5m < lowerBB_5m));
                if (cancelCausedByPrice)
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel lệnh do đã chạm Bollinger upper band (over bought) hoặc Bollinger lower band (over sold)");
                    return;
                }
            }            

            // Cancel các lệnh theo trending
            var cancelCausedByTrendCondition =
                (firstOrder.FromEntrySignal == SignalEntry_TrendingFull || firstOrder.FromEntrySignal == SignalEntry_TrendingHalf) // Lệnh vào theo trending
                &&
                ((IsBuying && waeExplosion_5m < waeDowntrend_5m && waeDeadVal_5m < waeDowntrend_5m) // Hiện tại có xu hướng bearish nhưng lệnh chờ là BUY
                ||
                (IsSelling && waeExplosion_5m < waeUptrend_5m && waeDeadVal_5m < waeUptrend_5m)); // Hiện tại có xu hướng bullish nhưng lệnh chờ là SELL
            if (cancelCausedByTrendCondition)
            {
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh do xu hướng hiện tại ngược với lệnh chờ");
                return;
            }

            #region Begin of move pending order
            var newPrice = GetSetPrice(currentTradeAction);
            
            var stopLossPrice = GetStopLossPrice(currentTradeAction, newPrice);

            var targetPrice_Half = GetTargetPrice_Half(currentTradeAction, newPrice);

            var targetPrice_Full = GetTargetPrice_Two(currentTradeAction, newPrice);

            if (State == State.Historical)
            {
                CancelAllPendingOrder();

                EnterOrder(currentTradeAction);
            }
            else if (State == State.Realtime)
            {
                foreach (var order in ActiveOrders.Values)
                {
                    try
                    {
                        LocalPrint($"Trying to modify waiting order {order.Name}, " +
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

                        filledPrice = newPrice;
                    }
                    catch (Exception ex)
                    {
                        LocalPrint($"ERROR: {ex.Message}");
                    }
                }
            }
            #endregion

        }

        /*
		This should be blank to easy to see the function
		*/
    }
}
