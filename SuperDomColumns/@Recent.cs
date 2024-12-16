// 
// Copyright (C) 2024, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui.SuperDom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

namespace NinjaTrader.NinjaScript.SuperDomColumns
{
	public class Recent : SuperDomColumn
	{
		private				ConcurrentDictionary<double, double>	askPriceValues;
		private				ConcurrentDictionary<double, double>	bidPriceValues;
		private				FontFamily								fontFamily;
		private				FontStyle								fontStyle;
		private				FontWeight								fontWeight;
		private				Pen										gridPen;			
		private				double									halfPenWidth;
		private				bool									heightUpdateNeeded;
		private				double									mostRecentLast;
		private				double									previousAsk					= double.MinValue;
		private				double									previousBid					= double.MinValue;
		private				ConcurrentDictionary<double, Timer>		resetAskTimers;
		private				ConcurrentDictionary<double, Timer>		resetBidTimers;
		private				double									textHeight;
		private				Typeface								typeFace;
		private				bool?									wasAskMostRecentlyFilled;

		public override void OnColumnLabelClicked(object sender, MouseButtonEventArgs e)
		{
			// Immediately reset when user clicks header
			foreach (KeyValuePair<double, Timer> kvp in resetAskTimers)
				kvp.Value.Dispose();
			resetAskTimers.Clear();

			foreach (KeyValuePair<double, Timer> kvp in resetBidTimers)
				kvp.Value.Dispose();
			resetBidTimers.Clear();

			askPriceValues.Clear();
			bidPriceValues.Clear();

			OnPropertyChanged();
		}

		protected override void OnMarketData(MarketDataEventArgs marketData)
		{
			if (State != State.Active) 
				return;

			if (marketData.MarketDataType == MarketDataType.Last)
			{
				double currentAsk = SuperDom.CurrentAsk;
				double currentBid = SuperDom.CurrentBid;
				if (marketData.Price.ApproxCompare(currentAsk) == 0)
				{
					askPriceValues.AddOrUpdate(marketData.Price, marketData.Volume, 
						(k, v) =>
						{
							if (ResetWhen == RecentResetWhen.PriceReturns)
							{
								// If the Ask moved away from this price and is now moving back, reset to the current volume
								if (currentAsk.ApproxCompare(mostRecentLast) != 0)
									return marketData.Volume;
								else
									return v + marketData.Volume;
							}
							else // AskBidChange assumes that volume will be reset by the timer when the Ask moves away, so we can safely assume we're just adding here
								return v + marketData.Volume;
						});

					mostRecentLast = marketData.Price;	
					wasAskMostRecentlyFilled = true;
				}
				else if (marketData.Price.ApproxCompare(currentBid) == 0)
				{
					bidPriceValues.AddOrUpdate(marketData.Price, marketData.Volume,
						(k, v) =>
						{
							if (ResetWhen == RecentResetWhen.PriceReturns)
							{
								// If the Bid moved away from this price and is now moving back, reset to the current volume
								if (currentBid.ApproxCompare(mostRecentLast) != 0)
									return marketData.Volume;
								else
									return v + marketData.Volume;
							}
							else // AskBidChange assumes that volume will be reset by the timer when the Bid moves away, so we can safely assume we're just adding here
								return v + marketData.Volume;
						});

					mostRecentLast = marketData.Price;
					wasAskMostRecentlyFilled = false;
				}
				else // Last filled between ask/bid
				{
					if (wasAskMostRecentlyFilled == true)
					{
						askPriceValues.AddOrUpdate(currentAsk, marketData.Volume,
							(k, v) =>
							{
								if (ResetWhen == RecentResetWhen.PriceReturns)
								{
									// If the Ask moved away from this price and is now moving back, reset to the current volume
									if (currentAsk.ApproxCompare(mostRecentLast) != 0)
										return marketData.Volume;
									else
										return v + marketData.Volume;
								}
								else // AskBidChange assumes that volume will be reset by the timer when the Ask moves away, so we can safely assume we're just adding here
									return v + marketData.Volume;
							});

						mostRecentLast = currentAsk;
						wasAskMostRecentlyFilled = true;
					}
					else if (wasAskMostRecentlyFilled == false)
					{
						bidPriceValues.AddOrUpdate(currentBid, marketData.Volume,
							(k, v) =>
							{
								if (ResetWhen == RecentResetWhen.PriceReturns)
								{
									// If the Bid moved away from this price and is now moving back, reset to the current volume
									if (currentBid.ApproxCompare(mostRecentLast) != 0)
										return marketData.Volume;
									else
										return v + marketData.Volume;
								}
								else // AskBidChange assumes that volume will be reset by the timer when the Bid moves away, so we can safely assume we're just adding here
									return v + marketData.Volume;
							});

						mostRecentLast = currentBid;
						wasAskMostRecentlyFilled = false;
					}
				}
			}

			if (ResetWhen == RecentResetWhen.PriceReturns)
				return;

			if (marketData.MarketDataType == MarketDataType.Ask)
			{
				// If price has moved away from old ask, then moved back, cancel the reset
				Timer oldTimer;
				if (resetAskTimers.TryRemove(marketData.Price, out oldTimer))
					oldTimer.Dispose();
				
				if (marketData.Price.ApproxCompare(previousAsk) != 0)
				{
					if (previousAsk != double.MinValue)
						resetAskTimers[previousAsk] = new Timer(ResetAsk, previousAsk, ResetTolerance, Timeout.Infinite);
					previousAsk = marketData.Price;
				}
			}
			else if (marketData.MarketDataType == MarketDataType.Bid)
			{
				// If price has moved away from old bid, then moved back, cancel the reset
				Timer oldTimer;
				if (resetBidTimers.TryRemove(marketData.Price, out oldTimer))
					oldTimer.Dispose();

				if (marketData.Price.ApproxCompare(previousBid) != 0)
				{
					if (previousBid != double.MinValue)
						resetBidTimers[previousBid] = new Timer(ResetBid, previousBid, ResetTolerance, Timeout.Infinite);
					previousBid = marketData.Price;
				}
			}
		}

		protected override void OnRender(DrawingContext dc, double renderWidth)
		{
			// This may be true if the UI for a column hasn't been loaded yet (e.g., restoring multiple tabs from workspace won't load each tab until it's clicked by the user)
			if (gridPen == null)
			{
				if (UiWrapper != null && PresentationSource.FromVisual(UiWrapper) != null)
				{
					Matrix m			= PresentationSource.FromVisual(UiWrapper).CompositionTarget.TransformToDevice;
					double dpiFactor	= 1 / m.M11;
					gridPen				= new Pen(Application.Current.TryFindResource("BorderThinBrush") as Brush,  1 * dpiFactor);
					halfPenWidth		= gridPen.Thickness * 0.5;
				}
			}

			if (fontFamily != SuperDom.Font.Family
				|| (SuperDom.Font.Italic && fontStyle != FontStyles.Italic)
				|| (!SuperDom.Font.Italic && fontStyle == FontStyles.Italic)
				|| (SuperDom.Font.Bold && fontWeight != FontWeights.Bold)
				|| (!SuperDom.Font.Bold && fontWeight == FontWeights.Bold))
			{
				// Only update this if something has changed
				fontFamily			= SuperDom.Font.Family;
				fontStyle			= SuperDom.Font.Italic ? FontStyles.Italic : FontStyles.Normal;
				fontWeight			= SuperDom.Font.Bold ? FontWeights.Bold : FontWeights.Normal;
				typeFace			= new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal);
				heightUpdateNeeded	= true;
			}
			double verticalOffset	= -gridPen.Thickness;
			double pixelsPerDip		= VisualTreeHelper.GetDpi(UiWrapper).PixelsPerDip;

			lock (SuperDom.Rows)
				foreach (PriceRow row in SuperDom.Rows)
				{
					if (renderWidth - halfPenWidth >= 0)
					{
						// Draw cell
						Rect rect = new Rect(-halfPenWidth, verticalOffset, renderWidth - halfPenWidth, SuperDom.ActualRowHeight);

						// Create a guidelines set
						GuidelineSet guidelines = new GuidelineSet();
						guidelines.GuidelinesX.Add(rect.Left	+ halfPenWidth);
						guidelines.GuidelinesX.Add(rect.Right	+ halfPenWidth);
						guidelines.GuidelinesY.Add(rect.Top		+ halfPenWidth);
						guidelines.GuidelinesY.Add(rect.Bottom	+ halfPenWidth);

						dc.PushGuidelineSet(guidelines);

						// Draw the Bid and Ask rectangles
						if (DisplayType == RecentDisplayType.BidAsk)
						{
							Rect bidRect = new Rect(-halfPenWidth, verticalOffset, (renderWidth / 2) - halfPenWidth, SuperDom.ActualRowHeight);
							Rect askRect = new Rect((renderWidth / 2) - halfPenWidth, verticalOffset, (renderWidth / 2) - halfPenWidth, SuperDom.ActualRowHeight);
							dc.DrawRectangle(BidBackColor, null, bidRect);
							dc.DrawRectangle(AskBackColor, null, askRect);
						}
						else if (DisplayType == RecentDisplayType.Ask)
						{
							dc.DrawRectangle(AskBackColor, null, rect);
						}
						else if (DisplayType == RecentDisplayType.Bid)
						{
							dc.DrawRectangle(BidBackColor, null, rect);
						}

						dc.DrawLine(gridPen, new Point(-gridPen.Thickness, rect.Bottom), new Point(renderWidth - halfPenWidth, rect.Bottom));
						dc.DrawLine(gridPen, new Point(rect.Right, verticalOffset), new Point(rect.Right, rect.Bottom));

						// Write bid/ask volumes
						if (SuperDom.IsConnected
							&& !SuperDom.IsReloading
							&& State == NinjaTrader.NinjaScript.State.Active)
						{
							if (DisplayType == RecentDisplayType.BidAsk || DisplayType == RecentDisplayType.Bid)
							{
								double bidVolume;
								if (bidPriceValues.TryGetValue(row.Price, out bidVolume))
								{
									fontFamily = SuperDom.Font.Family;
									typeFace = new Typeface(fontFamily, SuperDom.Font.Italic ? FontStyles.Italic : FontStyles.Normal, SuperDom.Font.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);

									if (renderWidth - 6 > 0)
									{
										FormattedText bidText = new FormattedText(bidVolume > 0 ? bidVolume.ToString(Core.Globals.GeneralOptions.CurrentCulture) : string.Empty, Core.Globals.GeneralOptions.CurrentCulture, FlowDirection.LeftToRight, typeFace, SuperDom.Font.Size, BidForeColor, pixelsPerDip) { MaxLineCount = 1, MaxTextWidth = (renderWidth / 2) - 6, Trimming = TextTrimming.CharacterEllipsis };
										// Getting the text height is expensive, so only update it if something's changed
										if (heightUpdateNeeded)
										{
											textHeight = bidText.Height;
											heightUpdateNeeded = false;
										}

										dc.DrawText(bidText, new Point(0 + 4, verticalOffset + (SuperDom.ActualRowHeight - textHeight) / 2));
									}
								}
							}

							if (DisplayType == RecentDisplayType.BidAsk || DisplayType == RecentDisplayType.Ask)
							{
								double askVolume;
								if (askPriceValues.TryGetValue(row.Price, out askVolume))
								{
									fontFamily = SuperDom.Font.Family;
									typeFace = new Typeface(fontFamily, SuperDom.Font.Italic ? FontStyles.Italic : FontStyles.Normal, SuperDom.Font.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);

									if (renderWidth - 6 > 0)
									{
										FormattedText askText = new FormattedText(askVolume > 0 ? askVolume.ToString(Core.Globals.GeneralOptions.CurrentCulture) : string.Empty, Core.Globals.GeneralOptions.CurrentCulture, FlowDirection.LeftToRight, typeFace, SuperDom.Font.Size, AskForeColor, pixelsPerDip) { MaxLineCount = 1, MaxTextWidth = (renderWidth / 2) - 6, Trimming = TextTrimming.CharacterEllipsis };
										// Getting the text height is expensive, so only update it if something's changed
										if (heightUpdateNeeded)
										{
											textHeight = askText.Height;
											heightUpdateNeeded = false;
										}

										dc.DrawText(askText, new Point(renderWidth / 2 + 4, verticalOffset + (SuperDom.ActualRowHeight - textHeight) / 2));
									}
								}
							}
						}

						dc.Pop();
						verticalOffset += SuperDom.ActualRowHeight;
					}
				}
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name							= Gui.Resource.NinjaScriptSuperDomColumnRecentLabel;
				Description						= Gui.Resource.NinjaScriptSuperDomColumnRecentDescription;
				DefaultWidth					= 100;
				PreviousWidth					= -1;
				IsDataSeriesRequired			= false;
				AskBackColor					= Application.Current.TryFindResource("brushPriceColumnBackground") as Brush;
				AskForeColor					= Application.Current.TryFindResource("brushVolumeColumnForeground") as SolidColorBrush;
				BidBackColor					= Application.Current.TryFindResource("brushPriceColumnBackground") as Brush;
				BidForeColor					= Application.Current.TryFindResource("brushVolumeColumnForeground") as SolidColorBrush;
				askPriceValues					= new ConcurrentDictionary<double, double>();
				bidPriceValues					= new ConcurrentDictionary<double, double>();
				DisplayType						= RecentDisplayType.BidAsk;
				resetAskTimers					= new ConcurrentDictionary<double, Timer>();
				resetBidTimers					= new ConcurrentDictionary<double, Timer>();
				ResetWhen						= RecentResetWhen.BidAskChange;
				ResetTolerance					= 2500;
			}
			else if (State == State.Configure)
			{
				if (UiWrapper != null && PresentationSource.FromVisual(UiWrapper) != null)
				{ 
					Matrix m			= PresentationSource.FromVisual(UiWrapper).CompositionTarget.TransformToDevice;
					double dpiFactor	= 1 / m.M11;
					gridPen				= new Pen(Application.Current.TryFindResource("BorderThinBrush") as Brush,  1 * dpiFactor);
					halfPenWidth		= gridPen.Thickness * 0.5;
				}
			}
			else if (State == State.Terminated)
			{
				foreach (KeyValuePair<double, Timer> kvp in resetAskTimers)
					kvp.Value.Dispose();
				resetAskTimers.Clear();

				foreach (KeyValuePair<double, Timer> kvp in resetBidTimers) 
					kvp.Value.Dispose();
				resetBidTimers.Clear();
			}
		}

		private void ResetAsk(object price)
		{
			double askPrice = (double)price;
			Timer oldTimer;
			if (resetAskTimers.TryRemove(askPrice, out oldTimer))
				oldTimer.Dispose();

			askPriceValues[askPrice] = 0;
			OnPropertyChanged();
		}

		private void ResetBid(object price)
		{
			double bidPrice = (double)price;
			Timer oldTimer;
			if (resetBidTimers.TryRemove(bidPrice, out oldTimer))
				oldTimer.Dispose();

			bidPriceValues[bidPrice] = 0; 
			OnPropertyChanged();
		}

		#region Properties
		#region Setup
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnDiplay", GroupName = "NinjaScriptSetup", Order = 100)]
		public RecentDisplayType DisplayType { get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnResetWhen", GroupName = "NinjaScriptSetup", Order = 110)]
		public RecentResetWhen ResetWhen { get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnResetTolerance", GroupName = "NinjaScriptSetup", Order = 115)]
		[Range(1, int.MaxValue)]
		public int ResetTolerance { get; set; }
		#endregion

		#region Colors
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnAskBackground", GroupName = "PropertyCategoryVisual", Order = 105)]
		public Brush AskBackColor { get; set; }

		[Browsable(false)]
		public string AskBackgroundBrushSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(AskBackColor, "brushAskPriceColumnBackground"); }
			set { AskBackColor = NinjaTrader.Gui.Serialize.StringToBrush(value, "brushAskPriceColumnBackground"); }
		}

		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnAskForeground", GroupName = "PropertyCategoryVisual", Order = 111)]
		public Brush AskForeColor { get; set; }

		[Browsable(false)]
		public string AskForeColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(AskForeColor); }
			set { AskForeColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnBidBackground", GroupName = "PropertyCategoryVisual", Order = 116)]
		public Brush BidBackColor { get; set; }

		[Browsable(false)]
		public string BidBackgroundBrushSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(BidBackColor, "brushBidPriceColumnBackground"); }
			set { BidBackColor = NinjaTrader.Gui.Serialize.StringToBrush(value, "brushBidPriceColumnBackground"); }
		}

		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptRecentColumnBidForeground", GroupName = "PropertyCategoryVisual", Order = 121)]
		public Brush BidForeColor { get; set; }

		[Browsable(false)]
		public string BidForeColorSerialize
		{
			get { return NinjaTrader.Gui.Serialize.BrushToString(BidForeColor); }
			set { BidForeColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
		}
		#endregion

		#endregion
	}

	[TypeConverter("NinjaTrader.Custom.ResourceEnumConverter")]
	public enum RecentDisplayType
	{
		Ask,
		Bid,
		BidAsk
	}

	[TypeConverter("NinjaTrader.Custom.ResourceEnumConverter")]
	public enum RecentResetWhen
	{
		PriceReturns,
		BidAskChange
	}
}
