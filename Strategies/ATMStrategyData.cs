using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NinjaTrader.Custom.Strategies
{
    public class ATMStrategyData
    {
        public bool IsVisible { get; set; }
        public bool AreLinesConfigurable { get; set; }
        public bool ArePlotsConfigurable { get; set; }
        public int BarsToLoad { get; set; }
        public bool DisplayInDataBox { get; set; }
        public DateTime From { get; set; }
        public int Panel { get; set; }
        public string ScaleJustification { get; set; }
        public bool ShowTransparentPlotsInDataBox { get; set; }
        public DateTime To { get; set; }
        public string Calculate { get; set; }
        public int Displacement { get; set; }
        public bool IsAutoScale { get; set; }
        public bool IsDataSeriesRequired { get; set; }
        public bool IsOverlay { get; set; }
        public string MaximumBarsLookBack { get; set; }
        public string Name { get; set; }
        public int SelectedValueSeries { get; set; }
        public int BarsRequiredToTrade { get; set; }
        public string Category { get; set; }
        public string ConnectionLossHandling { get; set; }
        public int DaysToLoad { get; set; }
        public int DefaultQuantity { get; set; }
        public int DisconnectDelaySeconds { get; set; }
        public int EntriesPerDirection { get; set; }
        public string EntryHandling { get; set; }
        public int ExitOnSessionCloseSeconds { get; set; }
        public bool IncludeCommission { get; set; }
        public bool IsAggregated { get; set; }
        public bool IsExitOnSessionCloseStrategy { get; set; }
        public bool IsFillLimitOnTouch { get; set; }
        public bool IsOptimizeDataSeries { get; set; }
        public bool IsStableSession { get; set; }
        public bool IsTickReplay { get; set; }
        public bool IsTradingHoursBreakLineVisible { get; set; }
        public bool IsWaitUntilFlat { get; set; }
        public int NumberRestartAttempts { get; set; }
        public int OptimizationPeriod { get; set; }
        public string OrderFillResolution { get; set; }
        public string OrderFillResolutionType { get; set; }
        public int OrderFillResolutionValue { get; set; }
        public int RestartsWithinMinutes { get; set; }
        public string SetOrderQuantity { get; set; }
        public int Slippage { get; set; }
        public string StartBehavior { get; set; }
        public string StopTargetHandling { get; set; }
        public bool SupportsOptimizationGraph { get; set; }
        public int TestPeriod { get; set; }
        public DateTime Gtd { get; set; }
        public string Template { get; set; }
        public string TimeInForce { get; set; }
        public string AtmSelector { get; set; }
        public int ReverseAtStopStrategyId { get; set; }
        public int ReverseAtTargetStrategyId { get; set; }
        public int ShadowStrategyStrategyId { get; set; }
        public int ChaseLimit { get; set; }
        public int EntryQuantity { get; set; }
        public int InitialTickSize { get; set; }
        public bool IsChase { get; set; }
        public bool IsChaseIfTouched { get; set; }
        public bool IsTargetChase { get; set; }
        public bool ReverseAtStop { get; set; }
        public bool ReverseAtTarget { get; set; }
        public bool UseMitForProfit { get; set; }
        public bool UseStopLimitForStopLossOrders { get; set; }

        [XmlArray("Brackets")]
        [XmlArrayItem("Bracket")]
        public List<Bracket> Brackets { get; set; }
    }

    public class Bracket
    {
        public int Quantity { get; set; }
        public int StopLoss { get; set; }
        public int Target { get; set; }

        public StopStrategy StopStrategy { get; set; }
    }

    public class StopStrategy
    {
        public int AutoBreakEvenPlus { get; set; }
        public int AutoBreakEvenProfitTrigger { get; set; }
        public bool IsSimStopEnabled { get; set; }
        public int VolumeTrigger { get; set; }
        public string Template { get; set; }
    }
}
