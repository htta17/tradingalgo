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

        protected AtmStrategy FullSizeAtmStrategy { get; set; }

        protected AtmStrategy HalfSizeAtmStrategy { get; set; }

        public PriceChangedATMBasedClass() : this("BASE")
        { 
        }

        public PriceChangedATMBasedClass(string logPrefix)
        {
            LogPrefix = logPrefix;
        }

        protected virtual void SetDefaultProperties()
        {
            
        }

        protected override void OnStateChange()
        {
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
                BarsRequiredToTrade = 10;
                IsInstantiatedOnEachOptimizationIteration = true;

                SetDefaultProperties();
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 3);
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
        }

        #region Helpers - Make sure not too many times OnBarUpdate is counted
        private DateTime lastExecutionTime_5m = DateTime.MinValue;
        private DateTime lastExecutionTime_3m = DateTime.MinValue;
        private DateTime lastExecutionTime_1m = DateTime.MinValue;

        int lastBar_5m = -1;
        int lastBar_3m = -1;
        int lastBar_1m = -1;

        bool triggerLastBar_1m = false;
        bool triggerLastBar_3m = false;
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
                }
                else if (DateTime.Now.Subtract(Time[0]).TotalSeconds > 59 && !triggerLastBar_1m)
                {
                    triggerLastBar_1m = true;

                    OnCurrentBarClose(barsPeriod);
                }
            }
            else if (barsPeriod == 3)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_3m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_3m = DateTime.Now;
                }
                else if (DateTime.Now.Subtract(Time[0]).TotalSeconds > 179 && !triggerLastBar_3m)
                {
                    triggerLastBar_3m = true;

                    OnCurrentBarClose(barsPeriod);
                }
            }
            else if (barsPeriod == 5)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_5m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_5m = DateTime.Now;
                }
                else if (DateTime.Now.Subtract(Time[0]).TotalSeconds > 259 && !triggerLastBar_5m)
                {
                    triggerLastBar_5m = true;

                    OnCurrentBarClose(barsPeriod);
                }
            }

            return isTooFast;
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
        protected abstract void OnNewBarOpen(int barsPeriod);

        protected abstract T1 ShouldTrade();

        protected abstract void EnterOrder(T1 action);

        /// <summary>
        /// Excecute when current bar close (last tick of current bar)
        /// </summary>
        /// <param name="barsPeriod"></param>
        protected abstract void OnCurrentBarClose(int barsPeriod);
        #endregion

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
                if (lastBar_1m != CurrentBar)
                {
                    triggerLastBar_1m = false; 
                    OnNewBarOpen(BarsPeriod.Value); 
                }               
                lastBar_1m = CurrentBar;
            }
            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 3) //3 minute
            {
                if (lastBar_3m != CurrentBar)
                {
                    OnNewBarOpen(BarsPeriod.Value);
                }
                lastBar_3m = CurrentBar;
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) //5 minute
            {
                if (lastBar_5m != CurrentBar)
                {
                    OnNewBarOpen(BarsPeriod.Value);
                }
                lastBar_5m = CurrentBar;
            }
        }
    }
}
