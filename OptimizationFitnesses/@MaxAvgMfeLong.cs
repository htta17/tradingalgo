// 
// Copyright (C) 2024, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

namespace NinjaTrader.NinjaScript.OptimizationFitnesses
{
	public class MaxAvgMfeLong : OptimizationFitness
	{
		protected override void OnCalculatePerformanceValue(StrategyBase strategy)
		{
			Value = strategy.SystemPerformance.LongTrades.TradesPerformance.Percent.AverageMfe;
		}

		protected override void OnStateChange()
		{               
			if (State == State.SetDefaults)
				Name = NinjaTrader.Custom.Resource.NinjaScriptOptimizationFitnessNameMaxAvgMfeLong;
		}
	}
}
