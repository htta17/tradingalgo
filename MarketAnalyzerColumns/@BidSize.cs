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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
#endregion

//This namespace holds Market Analyzer columns in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public class BidSize : MarketAnalyzerColumn
	{
		Cbi.InstrumentType instrumentType = InstrumentType.Unknown;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description				= NinjaTrader.Custom.Resource.NinjaScriptMarketAnalyzerColumnDescriptionBidSize;
				Name					= NinjaTrader.Custom.Resource.NinjaScriptMarketAnalyzerColumnNameBidSize;
				IsDataSeriesRequired	= false;
			}
		}

		protected override void OnMarketData(Data.MarketDataEventArgs marketDataUpdate)
		{
			if (marketDataUpdate.IsReset)
				CurrentValue	= double.MinValue;
			else if (marketDataUpdate.MarketDataType == Data.MarketDataType.Bid)
			{
				instrumentType	= marketDataUpdate.Instrument.MasterInstrument.InstrumentType;
				CurrentValue	= instrumentType == InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume(marketDataUpdate.Volume) : marketDataUpdate.Volume;
			}
		}

		#region Miscellaneous
		public override string Format(double value)
		{
			return value == double.MinValue
				? string.Empty
				: instrumentType == InstrumentType.CryptoCurrency
					? Core.Globals.FormatCryptocurrencyQuantity(value, false)
					: Core.Globals.FormatQuantity((long)value, false);
		}
		#endregion
	}
}
