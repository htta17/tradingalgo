using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    /**
     * Based Class cho các Strategies sử dụng tính toán khi đóng cây nến [OnBarClose]. Lưu ý các điểm sau: 
     * 1. Luôn luôn vào 2 order, 1 half size và 1 full size. Dịch stop loss khi break even hiện tại đang dựa khi số lượng order là 1
     */
    public abstract class BarClosedBaseClass<T> : NinjaTrader.NinjaScript.Strategies.Strategy        
    {
        private string LogPrefix { get; set; }
        public BarClosedBaseClass(string logPrefix)
        {
            LogPrefix = logPrefix;
        }

        public BarClosedBaseClass() : this("[BASED]")
        {
        }

        /// <summary>
        /// Thời gian có news, dừng trade trước và sau thời gian có news 5 phút. 
        /// Có 3 mốc quan trọng mặc định là 8:30am (Mở cửa Mỹ), 3:00pm (Đóng cửa Mỹ) và 5:00pm (Mở cửa châu Á).
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
        public string NewsTimeInput { get; set; } = "0830,1500,1700";


        protected List<int> NewsTimes = new List<int>();

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
            Order = 14,
            GroupName = "Parameters")]
        public bool AllowToMoveStopLossGain { get; set; } = true;

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Stop loss (Ticks):",
            Order = 15,
            GroupName = "Parameters")]
        public int StopLossInTicks { get; set; } = 120; // 25 points for MNQ

        /// <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Target 1 Profit (Ticks):",
            Order = 16,
            GroupName = "Parameters")]
        public int Target1InTicks { get; set; } = 60; // 25 points for MNQ


        // <summary>
        /// Số ticks cho stop loss khi đặt stoploss dựa theo BollingerBand
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Target 2 Profit (Ticks):",
            Order = 17,
            GroupName = "Parameters")]
        public int Target2InTicks { get; set; } = 120; // 25 points for MNQ

        

        /// <summary>
        /// Tự tính toán sizing và stop loss/target.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Tự tính toán sizing và stop loss/target",
            Order = 8,
            GroupName = "Parameters")]
        public bool AutoCalculateSizing { get; set; }

        /// <summary>
        /// Giá hiện tại cách target &lt; [PointToMoveTarget] thì di chuyển target.
        /// </summary>
        protected double PointToMoveTarget = 3;

        /// <summary>
        /// Giá hiện tại cách stop loss > [PointToMoveLoss] thì di chuyển stop loss.
        /// </summary>
        protected double PointToMoveLoss = 7;

        protected virtual void SetDefaultProperties()
        {
            MaximumDailyLoss = 400;
            DailyTargetProfit = 700;
            AllowToMoveStopLossGain = true;
            NewsTimeInput = "0830,1500,1700";

            StopLossInTicks = 120;
            Target1InTicks = 60;
            Target2InTicks = 120;

            AllowToMoveStopLossGain = true;            
            AutoCalculateSizing = false;

            PointToMoveTarget = 3;
            PointToMoveLoss = 7;
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Based Class for all Strategies which is triggered to execute with [Calculate] is [OnBarClose].";
                Name = "[BASED CLASS - NOT FOR RUNNING]";
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
                    NewsTimes = NewsTimeInput.Split(',').Select(c => int.Parse(c)).ToList();
                }
                catch (Exception e)
                {
                    Print($"[OnStateChange] - ERROR: " + e.Message);
                }
            }
        }

        protected override void OnBarUpdate()
        {
            //Add your custom strategy logic here.
        }

        /// <summary>
        /// Giá stop loss
        /// </summary>
        /// <param name="tradeAction">Cách trade: Mua hay bán, Trending hay Reverse</param>
        /// <param name="setPrice">Giá đặt lệnh</param>
        /// <returns></returns>
        protected virtual double GetStopLossPrice(T tradeAction, double setPrice)
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

        protected void LocalPrint(object val)
        {
            if (val.GetType() == typeof(string))
            {
                Print($"{LogPrefix}-{Time?[0]}-" + val);
            }
            else
            {
                Print(val);
            }
        }

        /// <summary>
        /// Giải thuật nào sử dụng thì implement hàm này
        /// </summary>
        /// <param name="tradeAction"></param>
        /// <param name="setPrice"></param>
        /// <returns></returns>
        protected abstract double GetTargetPrice_Half(T tradeAction, double setPrice);

        protected abstract double GetTargetPrice_Full(T tradeAction, double setPrice);

        protected abstract T ShouldTrade();

        protected void EnterOrderPure(double priceToSet, double target, double stoploss, string signal, int quantity, bool isBuying , bool isSelling )
        {
            var text = isBuying ? "LONG" : "SHORT";

            var allowTrade = (isBuying && priceToSet < target) || (isSelling && priceToSet > target);

            if (allowTrade)
            {
                if (isBuying)
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
        /// Dịch chuyển 1 stop loss hoặc target order
        /// </summary>
        /// <param name="newPrice">Giá mới cần chuyển đến</param>
        /// <param name="order">Order</param>
        /// <param name="isGainStop">isGainStop = true: Profit order, isGainStop = false : Profit order</param>
        /// <param name="buyOrSell">Lệnh này là bán hay mua (dùng cho logger nên không quá quan trọng)</param>
        /// <param name="fromEntrySignal">Entry Signal</param>
        protected void MoveTargetOrStopOrder(double newPrice, Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
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

        protected abstract double GetSetPrice(T tradeAction);
    }
}
