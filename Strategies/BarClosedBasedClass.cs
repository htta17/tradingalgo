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

        /// <summary>
        /// Current Trade action
        /// </summary>
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
        private TradingStatus ChickenStatus
        {
            get 
            {
                if (!SimpleActiveOrders.Any())
                {
                    return TradingStatus.Idle;
                }
                else if (SimpleActiveOrders.Values.Any(order => StrategiesUtilities.SignalEntries.Contains(order.Name)))
                {
                    return TradingStatus.PendingFill;
                }
                
                return TradingStatus.OrderExists;
            }
        }

        /// <summary>
        /// Realtime: Dùng order.Id làm key, không phải Realtime: Dùng Name làm key
        /// </summary>
        private Dictionary<string,Order> ActiveOrders = new Dictionary<string, Order>();
        
        private Dictionary<string, SimpleInfoOrder> SimpleActiveOrders = new Dictionary<string, SimpleInfoOrder>();

        #region Importants Configurations

        /// <summary>
        /// Điểm vào lệnh (Theo EMA29/51 hay Bollinger band)
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enter order price:",
            Order = 3,
            GroupName = "Importants Configurations")]
        public WayToTrade WayToTrade { get; set; } = WayToTrade.BollingerBand;
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
        /// Thời gian có news, dừng trade trước và sau thời gian có news 5 phút. 
        /// Có 3 mốc quan trọng mặc định là 8:30am (Mở cửa Mỹ), 3:00pm (Đóng cửa Mỹ) và 5:00pm (Mở cửa châu Á).
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
        public string NewsTimeInput { get; set; } = "0830,1500,1700";
        #endregion

        private List<int> NewsTimes = new List<int>();

        /// <summary>
        /// Giá hiện tại cách target &lt; [PointToMoveTarget] thì di chuyển target.
        /// </summary>
        private double PointToMoveTarget = 3;

        /// <summary>
        /// Giá hiện tại cách stop loss > [PointToMoveLoss] thì di chuyển stop loss.
        /// </summary>
        private double PointToMoveLoss = 7;

        /// <summary>
        /// Đưa hết các properties vào 1 nơi
        /// </summary>
        protected void SetDefaultProperties()
        {
            WayToTrade = WayToTrade.BollingerBand;

            MaximumDailyLoss = 400;
            DailyTargetProfit = 700;
            AllowToMoveStopLossGain = true;
            NewsTimeInput = "0830,1500,1700";

            PointToMoveTarget = 3;
            PointToMoveLoss = 7;
        }

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
                RealtimeErrorHandling = RealtimeErrorHandling.IgnoreAllErrors;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 30;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
                
                SetOrderQuantity = SetOrderQuantity.Strategy;
                DefaultQuantity = 2;
                // Set Properties

                SetDefaultProperties();
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
                    Print($"[OnStateChange] - ERROR: " + e.Message);
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
                    LocalPrint("[OnStateChange] - ERROR" + e.Message);
                }
            }
        }

        /// <summary>
        /// Kiểm tra điều kiện để vào lệnh
        /// </summary>
        /// <returns>Trade Action: Sell/Buy, Trending/Reverse</returns>
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
            if (waeDeadVal_5m < waeDowntrend_5m /*waeExplosion_5m < waeDowntrend_5m &&  && waeDeadVal_5m < waeExplosion_5m*/)
            {
                LocalPrint($"Found SELL signal (Trending) - waeDeadVal_5m: {waeDeadVal_5m:N2}, waeExplosion_5m: {waeExplosion_5m:N2}, waeDowntrend_5m: {waeDowntrend_5m:N2}");

                filledTime = Time[0];

                return TradeAction.Sell_Trending;
            }
            else if (waeDeadVal_5m < waeUptrend_5m /*waeExplosion_5m < waeUptrend_5m &&  && waeDeadVal_5m < waeExplosion_5m*/)
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
                    price = WayToTrade == WayToTrade.EMA2951 ? middleEMA : upperBB_5m;
                    break;

                case TradeAction.Buy_Reversal:
                    price = WayToTrade == WayToTrade.EMA2951 ? middleEMA : lowerBB_5m;
                    break;
            }            

            return Math.Round(price * 4, MidpointRounding.AwayFromZero) / 4.0;
        }        

        /// <summary>
        /// Giá stop loss
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
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

        /// <summary>
        /// Giá cho target 1 (Half)
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
        private double GetTargetPrice_Half(TradeAction tradeAction, double setPrice)
        {
            double price = -1;

            var time = ToTime(Time[0]);
            var isNightTime = time > 150000 || time < 083000;

            switch (tradeAction)
            {
                case TradeAction.Buy_Trending:
                    price = isNightTime // if night time, cut half at 7 point
                        ? setPrice + 7
                        : setPrice + (TickSize * StopLossInTicks / 2);
                    break;

                case TradeAction.Sell_Trending:
                    price = isNightTime // if night time, cut half at 7 point
                        ? setPrice - 7
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

        /// <summary>
        /// Giá cho target 2 (Full)
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
        private double GetTargetPrice_Full(TradeAction tradeAction, double setPrice)
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

        /// <summary>
        /// Đặt lệnh mua/bán
        /// </summary>
        /// <param name="tradeAction"></param>
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
            var targetFull = GetTargetPrice_Full(currentTradeAction, priceToSet);
            
            try
            {               
                var signalHalf = IsTrendingTrade ? StrategiesUtilities.SignalEntry_TrendingHalf : StrategiesUtilities.SignalEntry_ReversalHalf;
                EnterOrderPure(priceToSet, targetHalf, stopLossPrice, signalHalf, DefaultQuantity);
                
                var signalFull = IsTrendingTrade ? StrategiesUtilities.SignalEntry_TrendingFull : StrategiesUtilities.SignalEntry_ReversalFull;
                EnterOrderPure(priceToSet, targetFull, stopLossPrice, signalFull, DefaultQuantity);
            }
            catch (Exception ex) 
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        /// <summary>
        /// Move half price target dựa trên giá Bollinger Band Middle
        /// </summary>
        private void MoveTargetsBasedOnBollinger()
        {
            // Move target 1
            var targetHalfPriceOrders = ActiveOrders.Values.ToList().Where(c => c.FromEntrySignal == StrategiesUtilities.SignalEntry_ReversalHalf && 
                (c.OrderType == OrderType.StopMarket || c.OrderType == OrderType.StopLimit )).ToList();

            var len = targetHalfPriceOrders.Count;

            for (var i=0; i< len; i++)
            {
                var order = targetHalfPriceOrders[i];
                if ((IsBuying && middleBB_5m > filledPrice) || (IsSelling && middleBB_5m < filledPrice))
                {
                    MoveTargetOrStopOrder(middleBB_5m, order, true, IsBuying ? "BUY" : "SELL", order.FromEntrySignal);
                }                
            }

            // Move target 2            
            var targetFullPriceOrders = ActiveOrders.Values.ToList().Where(c => c.FromEntrySignal == StrategiesUtilities.SignalEntry_ReversalFull &&
                (c.OrderType == OrderType.StopMarket || c.OrderType == OrderType.StopLimit)).ToList();

            var lenFull = targetHalfPriceOrders.Count;

            for (var i = 0; i < lenFull; i++)
            {
                var order = targetFullPriceOrders[i];
                var newFullPrice = GetTargetPrice_Full(currentTradeAction, filledPrice); 

                if ((IsBuying && newFullPrice > filledPrice) || (IsSelling && newFullPrice < filledPrice))
                {
                    MoveTargetOrStopOrder(newFullPrice, order, true, IsBuying ? "BUY" : "SELL", order.FromEntrySignal);
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

                LocalPrint($"Dịch chuyển order {order.Name}, id: {order.Id}({text}), " +
                    $"{order.Quantity} contract(s) từ [{(isGainStop ? order.LimitPrice : order.StopPrice)}] " +
                    $"đến [{newPrice}] - {buyOrSell}");
            }
            catch (Exception ex)
            {
                LocalPrint($"[MoveTargetOrStopOrder] - ERROR: {ex.Message}");
            }
        }

        protected virtual void MoveStopOrder(Order stopOrder, double updatedPrice)
        {
            double newPrice = -1;
            var allowMoving = "";
            var stopOrderPrice = stopOrder.StopPrice;

            // Dịch stop loss lên break even 
            if (IsBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (startMovingStoploss && stopOrderPrice > filledPrice && stopOrderPrice + PointToMoveLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveLoss;
                    allowMoving = "BUY";
                }
                // Kéo về break even
                else if (stopOrderPrice < filledPrice && filledPrice + 1 < updatedPrice)
                {
                    newPrice = filledPrice + 1;
                    allowMoving = "BUY";
                }
            }
            else if (IsSelling)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (startMovingStoploss && stopOrderPrice < filledPrice && stopOrderPrice - PointToMoveLoss > updatedPrice)
                {
                    newPrice = updatedPrice + PointToMoveLoss;
                    allowMoving = "SELL";
                }
                // Kéo về break even
                else if (stopOrderPrice > filledPrice && filledPrice - 1 > updatedPrice)
                {
                    newPrice = filledPrice - 1;
                    allowMoving = "SELL";
                }
            }

            if (allowMoving != "")
            {
                LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{updatedPrice}]");

                MoveTargetOrStopOrder(newPrice, stopOrder, false, allowMoving, stopOrder.FromEntrySignal);
            }
        }

        protected virtual void MoveTargetOrder(Order targetOrder, double updatedPrice)
        {
            var targetOrderPrice = targetOrder.LimitPrice;

            // Dịch stop gain nếu giá quá gần target            
            if (IsBuying && updatedPrice + PointToMoveTarget > targetOrderPrice)
            {
                MoveTargetOrStopOrder(targetOrderPrice + PointToMoveTarget, targetOrder, true, "BUY", targetOrder.FromEntrySignal);

                startMovingStoploss = true;
            }
            else if (IsSelling && updatedPrice - PointToMoveTarget < targetOrderPrice)
            {
                MoveTargetOrStopOrder(targetOrderPrice - PointToMoveTarget, targetOrder, true, "SELL", targetOrder.FromEntrySignal);

                startMovingStoploss = true;
            }            
        }

        private void MoveTargetAndStopOrdersWithNewPrice(double updatedPrice)
        {
            if (!AllowToMoveStopLossGain)
            {
                LocalPrint("NOT allow to move stop loss/gain");
                return;
            }

            try
            {
                // Order với half price
                var hasHalfPriceOder = SimpleActiveOrders.Values.Any(order => order.FromEntrySignal == StrategiesUtilities.SignalEntry_ReversalHalf || order.FromEntrySignal == StrategiesUtilities.SignalEntry_TrendingHalf);

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
                        MoveStopOrder(stopOrder, updatedPrice);
                    }

                    var lenTarget = targetOrders.Count;
                    for (var i = 0; i < lenTarget; i++)
                    {
                        var targetOrder = targetOrders[i];
                        MoveTargetOrder(targetOrder, updatedPrice);
                    }
                }
            }
            catch (Exception e)
            {
                LocalPrint($"[OnMarketData] - ERROR: " + e.Message);
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

            if (ChickenStatus == TradingStatus.OrderExists)
            {
                MoveTargetAndStopOrdersWithNewPrice(updatedPrice);
            }
        }

        private readonly object lockOjbject = new Object();
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
                lock (lockOjbject)
                {
                    if (orderState == OrderState.Filled || orderState == OrderState.Cancelled)
                    {
                        ActiveOrders.Remove(key);
                        SimpleActiveOrders.Remove(key);
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
                if (ChickenStatus == TradingStatus.Idle)
                {
                    return;
                }
                else if (ChickenStatus == TradingStatus.PendingFill)
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

                if (State != State.Realtime)
                {
                    return;
                }

                if (ChickenStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if (shouldTrade != TradeAction.NoTrade)
                    {
                        EnterOrder(shouldTrade);
                    }
                }
                else if (ChickenStatus == TradingStatus.PendingFill)
                {
                    UpdatePendingOrder();
                }
                else if (ChickenStatus == TradingStatus.OrderExists)
                {
                    MoveTargetsBasedOnBollinger();
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
            var len = clonedList.Count;
            for (var i=0; i < len; i++)
            { 
                var order = clonedList[i];
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
            if (ChickenStatus == TradingStatus.OrderExists)
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
            else if (ChickenStatus == TradingStatus.PendingFill)
            {
                LocalPrint($"Transition to live, convert all pending fill orders to realtime");
                var clonedList = ActiveOrders.Values.ToList();
                var len = clonedList.Count;
                for (var i=0; i< len; i++)
                {
                    var order = clonedList[i];
                    GetRealtimeOrder(order);
                }
            }
        }

        /// <summary>
        /// Cập nhật giá trị cho các lệnh đang chờ, hoặc cancel do: Đợi lệnh quá 1h đồng hồ, do hết giờ trade, hoặc do 1 số điều kiện khác
        /// </summary>
        protected virtual void UpdatePendingOrder()
        {
            if (ChickenStatus != TradingStatus.PendingFill)
            {
                return;
            }

            #region Cancel lệnh nếu có 1 trong các điều kiện: 
            // Cancel lệnh do đợi quá lâu
            var firstOrder = ActiveOrders.First().Value;
            if ((Time[0] - filledTime).TotalMinutes > 60)
            {
                //Account.CancelAllOrders(Instrument);
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh do đợi quá lâu, Time[0]: {Time[0]}, filledTime: {filledTime}");
                return; 
            }

            // Cancel lệnh hết giờ trade
            if (ToTime(Time[0]) >= 150000 && ToTime(filledTime) < 150000)
            {
                //Account.CancelAllOrders(Instrument);
                CancelAllPendingOrder();
                LocalPrint($"Cancel lệnh hết giờ trade");
                return;
            }

            // Cancel cho lệnh theo đánh theo Bollinger (Ngược trend) 
            if (IsReverseTrade)
            {
                // Cancel khi cây nến đã vượt qua BB đường số 2 
                var cancelCausedByPrice = (firstOrder.IsLong && (highPrice_5m > upperStd2BB_5m || currentDEMA_5m > upperBB_5m)) 
                    || (firstOrder.IsShort && (lowPrice_5m < lowerStd2BB_5m || currentDEMA_5m < lowerBB_5m));
                if (cancelCausedByPrice)
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel lệnh do đã chạm Bollinger upper band (over bought) hoặc Bollinger lower band (over sold)");
                    return;
                }

                // Cancel nếu có 1 nguyên cây nến 5 phút vượt qua đường BB middle 
                var wholeCandlePassMiddleBand = (firstOrder.IsLong && lowPrice_5m > middleBB_5m) || 
                    (firstOrder.IsShort && highPrice_5m < middleBB_5m);
                if (wholeCandlePassMiddleBand)
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel vì đã có 1 cây nến 5 phút vượt qua được middle BB");
                    return;
                }
            }

            // Cancel các lệnh theo trending
            if (IsTrendingTrade) 
            {
                var cancelCausedByTrendCondition =
                    ((IsBuying && waeExplosion_5m < waeDowntrend_5m && waeDeadVal_5m < waeDowntrend_5m) // Hiện tại có xu hướng bearish nhưng lệnh chờ là BUY
                    ||
                    (IsSelling && waeExplosion_5m < waeUptrend_5m && waeDeadVal_5m < waeUptrend_5m)); // Hiện tại có xu hướng bullish nhưng lệnh chờ là SELL
                if (cancelCausedByTrendCondition)
                {
                    CancelAllPendingOrder();
                    LocalPrint($"Cancel lệnh do xu hướng hiện tại ngược với lệnh chờ");
                    return;
                }
            }
            #endregion

            #region Begin of move pending order
            var newPrice = GetSetPrice(currentTradeAction);
            
            var stopLossPrice = GetStopLossPrice(currentTradeAction, newPrice);

            var targetPrice_Half = GetTargetPrice_Half(currentTradeAction, newPrice);

            var targetPrice_Full = GetTargetPrice_Full(currentTradeAction, newPrice);

            if (State == State.Historical)
            {
                CancelAllPendingOrder();

                EnterOrder(currentTradeAction);
            }
            else if (State == State.Realtime)
            {
                var clonedList = ActiveOrders.Values.ToList();
                var len = clonedList.Count;

                for (var i= 0; i < len; i++)
                {
                    var order = clonedList[i];
                    try
                    {
                        LocalPrint($"Trying to modify waiting order [{order.Name}], " +
                            $"current Price: {order.LimitPrice}, current stop: {order.StopPrice}, " +
                            $"new Price: {newPrice:N2}, new stop loss: {stopLossPrice}");

                        ChangeOrder(order, order.Quantity, newPrice, order.StopPrice);

                        SetStopLoss(order.Name, CalculationMode.Price, stopLossPrice, false);

                        if (StrategiesUtilities.IsHalfPriceOrder(order))
                        {
                            SetProfitTarget(order.Name, CalculationMode.Price, targetPrice_Half, false);
                        }
                        else if (StrategiesUtilities.IsFullPriceOrder(order))
                        {
                            SetProfitTarget(order.Name, CalculationMode.Price, targetPrice_Full, false);
                        }

                        filledPrice = newPrice;
                    }
                    catch (Exception ex)
                    {
                        LocalPrint($"[UpdatePendingOrder] - ERROR: {ex.Message}");
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
