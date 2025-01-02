#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows.Media;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript;

#endregion

public class SRChannels : Indicator
{
    // Inputs
    private int pivotPeriod = 10;
    private string pivotSource = "High/Low"; // "Close/Open" is alternate
    private double channelWidthPercent = 5.0;
    private int minStrength = 1;
    private int maxSRLevels = 6;
    private int loopbackPeriod = 290;

    // Colors
    private Brush resistanceColor = Brushes.Red;
    private Brush supportColor = Brushes.Lime;
    private Brush inChannelColor = Brushes.Gray;

    // Internal variables
    private List<double> pivotValues = new List<double>();
    private List<int> pivotIndices = new List<int>();
    private List<Tuple<double, double>> srChannels = new List<Tuple<double, double>>();

    protected override void OnStateChange()
    {
        if (State == State.SetDefaults)
        {
            Description = "Support/Resistance Channels similar to TradingView's script";
            Name = "_SRChannels";
            Calculate = Calculate.OnBarClose;
            IsOverlay = true;
            AddPlot(Brushes.Transparent, "InvisiblePlot");
        }
    }

    protected override void OnBarUpdate()
    {
        if (CurrentBar < pivotPeriod * 2) return;

        // Calculate Pivots
        double pivotHigh = GetPivotHigh(pivotPeriod);
        double pivotLow = GetPivotLow(pivotPeriod);

        if (!double.IsNaN(pivotHigh))
        {
            pivotValues.Add(pivotHigh);
            pivotIndices.Add(CurrentBar);
        }
        if (!double.IsNaN(pivotLow))
        {
            pivotValues.Add(pivotLow);
            pivotIndices.Add(CurrentBar);
        }

        // Cleanup old pivots beyond loopback period
        CleanOldPivots();

        // Update SR Channels
        UpdateSRChannels();

        // Draw SR Channels
        DrawSRChannels();
    }

    private double GetPivotHigh(int period)
    {
        if (CurrentBar < period * 2) return double.NaN;
        for (int i = 1; i <= period; i++)
        {
            if (High[0] <= High[i] || High[0] <= High[-i])
                return double.NaN;
        }
        return High[0];
    }

    private double GetPivotLow(int period)
    {
        if (CurrentBar < period * 2) return double.NaN;
        for (int i = 1; i <= period; i++)
        {
            if (Low[0] >= Low[i] || Low[0] >= Low[-i])
                return double.NaN;
        }
        return Low[0];
    }

    private void CleanOldPivots()
    {
        int cutoffBar = CurrentBar - loopbackPeriod;
        for (int i = pivotIndices.Count - 1; i >= 0; i--)
        {
            if (pivotIndices[i] < cutoffBar)
            {
                pivotValues.RemoveAt(i);
                pivotIndices.RemoveAt(i);
            }
        }
    }

    private void UpdateSRChannels()
    {
        srChannels.Clear();
        for (int i = 0; i < pivotValues.Count; i++)
        {
            double high = pivotValues[i];
            double low = pivotValues[i];
            int strength = 0;

            // Determine channel range
            for (int j = 0; j < pivotValues.Count; j++)
            {
                double value = pivotValues[j];
                if (value >= low && value <= high)
                {
                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                    strength++;
                }
            }

            if (strength >= minStrength)
                srChannels.Add(Tuple.Create(high, low));
        }

        // Limit number of channels
        srChannels = srChannels.GetRange(0, Math.Min(srChannels.Count, maxSRLevels));
    }

    private void DrawSRChannels()
    {
        for (int i = 0; i < srChannels.Count; i++)
        {
            double high = srChannels[i].Item1;
            double low = srChannels[i].Item2;

            string tag = $"SRChannel_{i}";
            RemoveDrawObject(tag);
            Draw.Rectangle(this, tag, false, pivotIndices[0], high, CurrentBar, low, inChannelColor, resistanceColor, 2);
        }
    }
}
