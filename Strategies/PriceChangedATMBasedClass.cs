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
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T1">Should Trade answer</typeparam>
    public abstract class PriceChangedATMBasedClass<T1> : Strategy
    {
        private string LogPrefix { get; set; }        
        protected const string OrderEntryName = "Entry";
        protected const string OrderStopName = "Stop";
        protected const string OrderTargetName = "Target";

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

        protected TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        public PriceChangedATMBasedClass() : this("BASE")
        { 
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
        /// Do with regular tick
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

        protected abstract void EnterOrderHistorical(T1 action);

        protected abstract void EnterOrderRealtime(T1 action); 

        private void EnterOrder(T1 action)
        {
            if (State == State.Historical)
            {
                // Enter với stop loss và stop gain 
                EnterOrderHistorical(action);
            }
            else 
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
