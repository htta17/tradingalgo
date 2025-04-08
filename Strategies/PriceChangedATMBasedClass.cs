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

                    LocalPrint($"{Time[0]} - {DateTime.Now} - {Time[0].Subtract(DateTime.Now).TotalSeconds:N0} - {barsPeriod}");

                    if (Time[0].Subtract(DateTime.Now).TotalSeconds < 1 && !triggerLastBar_1m)
                    {
                        triggerLastBar_1m = true;

                        OnCurrentBarClosed(barsPeriod);
                    }
                }                 
            }
            else if (barsPeriod == 3)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_3m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_3m = DateTime.Now;

                    LocalPrint($"{Time[0]} - {DateTime.Now} - {Time[0].Subtract(DateTime.Now).TotalSeconds:N0} - {barsPeriod}");

                    if (Time[0].Subtract(DateTime.Now).TotalSeconds < 1 && !triggerLastBar_3m)
                    {
                        triggerLastBar_3m = true;

                        OnCurrentBarClosed(barsPeriod);
                    }
                }                 
            }
            else if (barsPeriod == 5)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_5m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_5m = DateTime.Now;

                    LocalPrint($"{Time[0]} - {DateTime.Now} - {Time[0].Subtract(DateTime.Now).TotalSeconds:N0} - {barsPeriod}");

                    if (Time[0].Subtract(DateTime.Now).TotalSeconds < 1 && !triggerLastBar_5m)
                    {
                        triggerLastBar_5m = true;

                        OnCurrentBarClosed(barsPeriod);
                    }
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
        protected abstract void OnNewBarCreated(int barsPeriod);

        protected abstract T1 ShouldTrade();

        protected abstract void EnterOrder(T1 action);

        /// <summary>
        /// Excecute when current bar close (last tick of current bar)
        /// </summary>
        /// <param name="barsPeriod"></param>
        protected abstract void OnCurrentBarClosed(int barsPeriod);
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
                    OnNewBarCreated(BarsPeriod.Value); 
                }               
                lastBar_1m = CurrentBar;
            }
            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 3) //3 minute
            {
                if (lastBar_3m != CurrentBar)
                {
                    OnNewBarCreated(BarsPeriod.Value);
                }
                lastBar_3m = CurrentBar;
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) //5 minute
            {
                if (lastBar_5m != CurrentBar)
                {
                    OnNewBarCreated(BarsPeriod.Value);
                }
                lastBar_5m = CurrentBar;
            }
        }
    }
}
