// 
// Copyright (C) 2024, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.NinjaScript;

#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
	public abstract class ChartMarker : DrawingTool
	{
		private		Brush					areaBrush;
		[CLSCompliant(false)]
		protected readonly DeviceBrush		AreaDeviceBrush		= new();
		private		Brush					outlineBrush;
		[CLSCompliant(false)]
		protected readonly DeviceBrush		OutlineDeviceBrush	= new();

		public ChartAnchor	Anchor					{ get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolShapesAreaBrush", GroupName = "NinjaScriptGeneral", Order = 1)]
		[XmlIgnore]
		public Brush		AreaBrush
		{
			get => areaBrush;
			set 
			{
				areaBrush = value;
				AreaDeviceBrush.Brush = value;
			}
		}

		[Display(ResourceType = typeof(Resource), Name = "NinjaScriptDrawingToolShapesSize", GroupName = "GuiGeneral", Order = 2)]
		public ChartMarkerSize		Size { get; set; } = ChartMarkerSize.Medium;

		[Browsable(false)]
		public string AreaBrushSerialize
		{
			get => Serialize.BrushToString(AreaBrush);
			set => AreaBrush = Serialize.StringToBrush(value);
		}

		protected double BarWidth
		{
			get
			{
				if (AttachedTo != null)
				{
					ChartBars chartBars = AttachedTo.ChartObject as ChartBars;
					if (chartBars == null && AttachedTo.ChartObject is IChartBars iChartBars)
						chartBars = iChartBars.ChartBars;
					if (chartBars?.Properties.ChartStyle != null)
						return chartBars.Properties.ChartStyle.BarWidth;
				}
				return MinimumSize;
			}
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolShapesOutlineBrush", GroupName = "NinjaScriptGeneral", Order = 3)]
		[XmlIgnore]
		public Brush		OutlineBrush
		{
			get => outlineBrush;
			set 
			{
				outlineBrush = value;
				OutlineDeviceBrush.Brush = value;
			}
		}

		[Browsable(false)]
		public string OutlineBrushSerialize
		{
			get => Serialize.BrushToString(OutlineBrush);
			set => OutlineBrush = Serialize.StringToBrush(value);
		}

		protected static float MinimumSize => 5f;

		public override IEnumerable<ChartAnchor> Anchors => new[]{ Anchor };

		public override void OnCalculateMinMax()
		{
			MinValue = double.MaxValue;
			MaxValue = double.MinValue;

			if (!IsVisible)
				return;


			MinValue = Anchor.Price;
			MaxValue = Anchor.Price;
		}

		protected float GetSizeMultiplier() => Size switch	{
																ChartMarkerSize.ExtraLarge	=> 2.0f,
																ChartMarkerSize.Large		=> 1.5f,
																ChartMarkerSize.Small		=> 0.75f,
																ChartMarkerSize.ExtraSmall	=> 0.5f,
																_							=> 1f
															};

		protected override void Dispose(bool disposing)
		{
			AreaDeviceBrush.RenderTarget	= null;
			OutlineDeviceBrush.RenderTarget	= null;
		}

		public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, Point point)
		{
			if (DrawingState == DrawingState.Building)
				return Cursors.Pen;
			if (DrawingState == DrawingState.Moving)
				return IsLocked ? Cursors.No : Cursors.SizeAll;
			// this is fired whenever the chart marker is selected.
			// so if the mouse is anywhere near our marker, show a moving icon only. point is already in device pixels
			// we want to check at least 6 pixels away, or by padding x 2 if its more (It could be 0 on some objects like square)
			Point anchorPointPixels = Anchor.GetPoint(chartControl, chartPanel, chartScale);
			Vector distToMouse = point - anchorPointPixels;
			return distToMouse.Length <= GetSelectionSensitivity() ? IsLocked ?  Cursors.Arrow : Cursors.SizeAll : null;
		}

		public override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return Array.Empty<Point>();

			ChartPanel	chartPanel	= chartControl.ChartPanels[chartScale.PanelIndex];
			Point		anchorPoint	= Anchor.GetPoint(chartControl, chartPanel, chartScale);
			return new[]{ anchorPoint };
		}

		private double GetSelectionSensitivity() => Math.Max(15d, 10d * (BarWidth / 5d));

		public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
		{
			if (DrawingState == DrawingState.Building)
				return false;
			// we have a single anchor so this is pretty easy
			if (!IsAutoScale && (Anchor.Price < chartScale.MinValue || Anchor.Price > chartScale.MaxValue))
				return false;
			return Anchor.Time >= firstTimeOnChart && Anchor.Time <= lastTimeOnChart;
		}

		public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			switch (DrawingState)
			{
				case DrawingState.Building:
					dataPoint.CopyDataValues(Anchor);
					Anchor.IsEditing	= false;
					DrawingState		= DrawingState.Normal;
					IsSelected			= false;
					break;
				case DrawingState.Normal:
					// make sure they clicked near us. use GetCursor in case something has more than one point, like arrows
					Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
					if (GetCursor(chartControl, chartPanel, chartScale, point) != null)
						DrawingState = DrawingState.Moving;
					else
						IsSelected = false;
					break;
			}
		}

		public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (DrawingState != DrawingState.Moving || IsLocked && DrawingState != DrawingState.Building)
				return;
			dataPoint.CopyDataValues(Anchor);
		}

		public override void OnMouseUp(ChartControl control, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (DrawingState is DrawingState.Editing or DrawingState.Moving)
				DrawingState = DrawingState.Normal;
		}
	}

	[TypeConverter(typeof(Core.CoreEnumConverter))]
	public enum ChartMarkerSize
	{
		ExtraSmall			= 0,
		Small				= 1,
		Medium				= 2,
		Large				= 3,
		ExtraLarge			= 4
	}

	/// <summary>
	/// Represents an interface that exposes information regarding a Dot IDrawingTool.
	/// </summary>
	public class Dot : ChartMarker
	{
		public override object Icon => Gui.Tools.Icons.DrawDot;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName = Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true,
				};
				Name			= Custom.Resource.NinjaScriptDrawingToolsChartDotMarkerName;
				AreaBrush		= Brushes.DodgerBlue;
				OutlineBrush	= Brushes.DarkGray;
			}
			else if (State == State.Terminated)
				Dispose();
		}

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return;

			ChartPanel	panel					= chartControl.ChartPanels[chartScale.PanelIndex];
			Point		pixelPoint				= Anchor.GetPoint(chartControl, panel, chartScale);

			AreaDeviceBrush.RenderTarget		= RenderTarget;
			OutlineDeviceBrush.RenderTarget		= RenderTarget;
			RenderTarget.AntialiasMode			= SharpDX.Direct2D1.AntialiasMode.PerPrimitive;

			float radius = Math.Max((float) BarWidth, MinimumSize) * GetSizeMultiplier();
			// center rendering on anchor is done by radius method of drawing here
			SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AreaDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.FillEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2((float)pixelPoint.X, (float)pixelPoint.Y), radius, radius), tmpBrush);
			tmpBrush = IsInHitTest ? chartControl.SelectionBrush : OutlineDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.DrawEllipse(new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2((float)pixelPoint.X, (float)pixelPoint.Y), radius, radius), tmpBrush);
		}
	}

	/// <summary>
	/// Represents an interface that exposes information regarding a Square IDrawingTool.
	/// </summary>
	public class Square : ChartMarker
	{
		protected void DrawSquare(float width, ChartControl chartControl, ChartScale chartScale)
		{
			AreaDeviceBrush.RenderTarget = RenderTarget;
			OutlineDeviceBrush.RenderTarget = RenderTarget;

			ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];
			Point pixelPoint = Anchor.GetPoint(chartControl, panel, chartScale);

			// adjust our x/y to center the rect on our anchor (moving the top left back and up by half)
			float xCentered = (float)(pixelPoint.X - width / 2f);
			float yCentered = (float)(pixelPoint.Y - width / 2f);

			SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AreaDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.FillRectangle(new SharpDX.RectangleF(xCentered, yCentered, width, width), tmpBrush);
			tmpBrush = IsInHitTest ? chartControl.SelectionBrush : OutlineDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.DrawRectangle(new SharpDX.RectangleF(xCentered, yCentered, width, width), tmpBrush);
		}

		public override object Icon => Gui.Tools.Icons.DrawSquare;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName = Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true
				};
				Name				= Custom.Resource.NinjaScriptDrawingToolsChartSquareMarkerName;
				AreaBrush			= Brushes.Crimson;
				OutlineBrush		= Brushes.DarkGray;
			}
			else if (State == State.Terminated)
				Dispose();
		}

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return;
			float barWidth = Math.Max((float)BarWidth * 2, MinimumSize * 2) * GetSizeMultiplier(); // we draw from center
			DrawSquare(barWidth, chartControl, chartScale);
		}
	}

	/// <summary>
	/// Represents an interface that exposes information regarding a Diamond IDrawingTool.
	/// </summary>
	public class Diamond : Square
	{
		public override object Icon => Gui.Tools.Icons.DrawDiamond;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName = Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true,
				};
				Name					= Custom.Resource.NinjaScriptDrawingToolsChartDiamondMarkerName;
				AreaBrush				= Brushes.Crimson;
				OutlineBrush			= Brushes.DarkGray;
			}
			else if (State == State.Terminated)
				Dispose();
		}

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return;

			ChartPanel panel	= chartControl.ChartPanels[chartScale.PanelIndex];
			Point pixelPoint	= Anchor.GetPoint(chartControl, panel, chartScale);

			// rotate it 45 degrees and bam, a diamond
			// rotate from anchor since that will be center of rendering in base render
			RenderTarget.Transform	= SharpDX.Matrix3x2.Rotation(MathHelper.DegreesToRadians(45), pixelPoint.ToVector2());

			AreaDeviceBrush.RenderTarget	= RenderTarget;
			OutlineDeviceBrush.RenderTarget	= RenderTarget;

			float barWidth = Math.Max((float)BarWidth * 2, MinimumSize * 2); // we draw from center

			// We are rotating this square to make a diamond, so we need the distance from opposite angles to be bar width
			// Using barWidth as the hypotenuse, calculate equal side lengths of a right triangle
			float hypotenuseAdjustedWidth = (float)Math.Sqrt(Math.Pow(barWidth, 2) * 0.5) * GetSizeMultiplier();
			DrawSquare(hypotenuseAdjustedWidth, chartControl, chartScale);

			RenderTarget.Transform	= SharpDX.Matrix3x2.Identity;
		}
	}

	public abstract class ArrowMarkerBase : ChartMarker
	{
		[XmlIgnore]
		[Browsable(false)]
		protected bool		IsUpArrow	{ get; set; }

		public override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return Array.Empty<Point>();
			ChartPanel panel			= chartControl.ChartPanels[chartScale.PanelIndex];
			Point pixelPointArrowTop	= Anchor.GetPoint(chartControl, panel, chartScale);
			return new [] { new Point(pixelPointArrowTop.X, pixelPointArrowTop.Y) };
		}

		public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (DrawingState != DrawingState.Moving || IsLocked)
				return;

			// this is reversed, we're pulling into arrow
			Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
			Anchor.UpdateFromPoint(new Point(point.X, point.Y), chartControl, chartScale);
		}

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return;

			AreaDeviceBrush.RenderTarget = RenderTarget;
			OutlineDeviceBrush.RenderTarget = RenderTarget;

			ChartPanel panel			= chartControl.ChartPanels[chartScale.PanelIndex];
			Point pixelPoint			= Anchor.GetPoint(chartControl, panel, chartScale);
			SharpDX.Vector2 endVector	= pixelPoint.ToVector2();

			// the geometry is created with 0,0 as point origin, and pointing UP by default.
			// so translate & rotate as needed
			SharpDX.Matrix3x2 transformMatrix;
			if (!IsUpArrow)
			{
				// Flip it around. beware due to our translation we rotate on origin
				transformMatrix = /*SharpDX.Matrix3x2.Scaling(arrowScale, arrowScale) **/ SharpDX.Matrix3x2.Rotation(MathHelper.DegreesToRadians(180), SharpDX.Vector2.Zero) * SharpDX.Matrix3x2.Translation(endVector);
			}
			else 
				transformMatrix = /*SharpDX.Matrix3x2.Scaling(arrowScale, arrowScale) **/ SharpDX.Matrix3x2.Translation(endVector);

			RenderTarget.AntialiasMode	= SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
			RenderTarget.Transform		= transformMatrix;
			
			float barWidth			= Math.Max((float) BarWidth, MinimumSize) * GetSizeMultiplier();
			float arrowHeight		= barWidth * 3f;
			float arrowPointHeight	= barWidth;
			float arrowStemWidth	= barWidth / 3f;

			SharpDX.Direct2D1.PathGeometry arrowPathGeometry = new(Core.Globals.D2DFactory);
			SharpDX.Direct2D1.GeometrySink geometrySink = arrowPathGeometry.Open();
			geometrySink.BeginFigure(SharpDX.Vector2.Zero, SharpDX.Direct2D1.FigureBegin.Filled);

			geometrySink.AddLine(new SharpDX.Vector2(barWidth, arrowPointHeight));
			geometrySink.AddLine(new SharpDX.Vector2(arrowStemWidth, arrowPointHeight));
			geometrySink.AddLine(new SharpDX.Vector2(arrowStemWidth, arrowHeight));
			geometrySink.AddLine(new SharpDX.Vector2(-arrowStemWidth, arrowHeight));
			geometrySink.AddLine(new SharpDX.Vector2(-arrowStemWidth, arrowPointHeight));
			geometrySink.AddLine(new SharpDX.Vector2(-barWidth, arrowPointHeight));

			geometrySink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
			geometrySink.Close(); // note this calls dispose for you. but not the other way around

			SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AreaDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.FillGeometry(arrowPathGeometry, tmpBrush);
			tmpBrush = IsInHitTest ? chartControl.SelectionBrush : OutlineDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.DrawGeometry(arrowPathGeometry, tmpBrush);
			arrowPathGeometry.Dispose();
			RenderTarget.Transform				= SharpDX.Matrix3x2.Identity;
		}
	}

	/// <summary>
	/// Represents an interface that exposes information regarding an Arrow Down IDrawingTool.
	/// </summary>
	public class ArrowDown : ArrowMarkerBase
	{
		public override object Icon => Gui.Tools.Icons.DrawArrowDown;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName = Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true,
				};
				Name				= Custom.Resource.NinjaScriptDrawingToolsChartArrowDownMarkerName;
				AreaBrush			= Brushes.Crimson;
				OutlineBrush		= Brushes.DarkGray;
				IsUpArrow			= false;
			}
			else if (State == State.Terminated)
				Dispose();
		}
	}

	/// <summary>
	/// Represents an interface that exposes information regarding an Arrow Up IDrawingTool.
	/// </summary>
	public class ArrowUp : ArrowMarkerBase
	{
		public override object Icon => Gui.Tools.Icons.DrawArrowUp;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName = Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true,
				};
				Name			= Custom.Resource.NinjaScriptDrawingToolsChartArrowUpMarkerName;
				AreaBrush		= Brushes.SeaGreen;
				OutlineBrush	= Brushes.DarkGray;
				IsUpArrow		= true;
			}
			else if (State == State.Terminated)
				Dispose();
		}
	}

	public abstract class TriangleBase : ChartMarker
	{
		[XmlIgnore]
		[Browsable(false)]
		public bool		IsUpTriangle	{ get; protected set; }

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Anchor.IsEditing)
				return;

			AreaDeviceBrush.RenderTarget = RenderTarget;
			OutlineDeviceBrush.RenderTarget = RenderTarget;

			ChartPanel		panel		= chartControl.ChartPanels[chartScale.PanelIndex];
			Point			pixelPoint	= Anchor.GetPoint(chartControl, panel, chartScale);
			SharpDX.Vector2	endVector	= pixelPoint.ToVector2();

			// the geometry is created with 0,0 as point origin, and pointing UP by default.
			// so translate & rotate as needed
			SharpDX.Matrix3x2 transformMatrix;
			
			if (IsUpTriangle)
				transformMatrix = SharpDX.Matrix3x2.Translation(endVector);
			else 
				transformMatrix = SharpDX.Matrix3x2.Rotation(MathHelper.DegreesToRadians(180), SharpDX.Vector2.Zero) * SharpDX.Matrix3x2.Translation(endVector);
			
			RenderTarget.AntialiasMode	= SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
			RenderTarget.Transform		= transformMatrix;

			SharpDX.Direct2D1.PathGeometry trianglePathGeometry	= new(Core.Globals.D2DFactory);
			SharpDX.Direct2D1.GeometrySink geometrySink			= trianglePathGeometry.Open();

			float barWidth = Math.Max((float) BarWidth, MinimumSize) * GetSizeMultiplier();
			geometrySink.BeginFigure(SharpDX.Vector2.Zero, SharpDX.Direct2D1.FigureBegin.Filled);
			geometrySink.AddLine(new SharpDX.Vector2(barWidth, barWidth));
			geometrySink.AddLine(new SharpDX.Vector2(-barWidth, barWidth));
			geometrySink.AddLine(SharpDX.Vector2.Zero);// cap off figure
			geometrySink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
			geometrySink.Close(); // note this calls dispose for you. but not the other way around

			SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : OutlineDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.DrawGeometry(trianglePathGeometry, tmpBrush);
			tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AreaDeviceBrush.BrushDX;
			if (tmpBrush != null)
				RenderTarget.FillGeometry(trianglePathGeometry, tmpBrush);
			
			trianglePathGeometry.Dispose();
			RenderTarget.Transform = SharpDX.Matrix3x2.Identity;
		}
	}

	/// <summary>
	/// Represents an interface that exposes information regarding a Triangle Down IDrawingTool.
	/// </summary>
	public class TriangleDown : TriangleBase
	{
		public override object Icon => Gui.Tools.Icons.DrawTriangleDown;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName	= Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true,
				};
				Name				= Custom.Resource.NinjaScriptDrawingToolsChartTriangleDownMarkerName;
				AreaBrush			= Brushes.Crimson;
				OutlineBrush		= Brushes.DarkGray;
				IsUpTriangle		= false;
			}
			else if (State == State.Terminated)
				Dispose();
		}
	}

	/// <summary>
	/// Represents an interface that exposes information regarding a Triangle Up IDrawingTool.
	/// </summary>
	public class TriangleUp : TriangleBase
	{
		public override object Icon => Gui.Tools.Icons.DrawTriangleUp;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Anchor	= new ChartAnchor
				{
					DisplayName = Custom.Resource.NinjaScriptDrawingToolAnchor,
					IsEditing	= true,
				};
				Name			= Custom.Resource.NinjaScriptDrawingToolsChartTriangleUpMarkerName;
				AreaBrush		= Brushes.SeaGreen;
				OutlineBrush	= Brushes.DarkGray;
				IsUpTriangle	= true;
			}
			else if (State == State.Terminated)
				Dispose();
		}
	}

	public static partial class Draw
	{
		// this function does all the actual instance creation and setup
		private static T ChartMarkerCore<T>(NinjaScriptBase owner, string tag, bool isAutoScale,
										int barsAgo, DateTime time, double yVal, Brush brush, bool isGlobal, string templateName) where T : ChartMarker
		{
			if (owner == null)
				throw new ArgumentException("owner");
			if (time == Core.Globals.MinDate && barsAgo == int.MinValue)
				throw new ArgumentException("bad start/end date/time");
			if (yVal.ApproxCompare(double.MinValue) == 0 || yVal.ApproxCompare(double.MaxValue) == 0)
				throw new ArgumentException("bad Y value");

			if (isGlobal && tag[0] != GlobalDrawingToolManager.GlobalDrawingToolTagPrefix)
				tag = $"{GlobalDrawingToolManager.GlobalDrawingToolTagPrefix}{tag}";

			if (DrawingTool.GetByTagOrNew(owner, typeof(T), tag, templateName) is not T chartMarkerT)
				return default;

			DrawingTool.SetDrawingToolCommonValues(chartMarkerT, tag, isAutoScale, owner, isGlobal);
			
			ChartAnchor anchor = DrawingTool.CreateChartAnchor(owner, barsAgo, time, yVal);
			anchor.CopyDataValues(chartMarkerT.Anchor);

			// don't forget to set anchor as not editing or else it won't be drawn
			chartMarkerT.Anchor.IsEditing = false;

			// can be null when loaded from templateName
			if (brush != null)
				chartMarkerT.AreaBrush = brush;

			chartMarkerT.SetState(State.Active);
			return chartMarkerT;
		}

		// arrow down
		/// <summary>
		/// Draws an arrow pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static ArrowDown ArrowDown(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<ArrowDown>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws an arrow pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static ArrowDown ArrowDown(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush) 
			=> ChartMarkerCore<ArrowDown>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws an arrow pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static ArrowDown ArrowDown(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<ArrowDown>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws an arrow pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static ArrowDown ArrowDown(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<ArrowDown>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws an arrow pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static ArrowDown ArrowDown(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<ArrowDown>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws an arrow pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static ArrowDown ArrowDown(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<ArrowDown>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		// arrow up
		/// <summary>
		/// Draws an arrow pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static ArrowUp ArrowUp(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<ArrowUp>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws an arrow pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static ArrowUp ArrowUp(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush)
			=> ChartMarkerCore<ArrowUp>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws an arrow pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static ArrowUp ArrowUp(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<ArrowUp>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws an arrow pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static ArrowUp ArrowUp(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<ArrowUp>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws an arrow pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static ArrowUp ArrowUp(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<ArrowUp>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws an arrow pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static ArrowUp ArrowUp(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<ArrowUp>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		// diamond
		/// <summary>
		/// Draws a diamond.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static Diamond Diamond(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<Diamond>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws a diamond.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static Diamond Diamond(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush)
			=> ChartMarkerCore<Diamond>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws a diamond.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static Diamond Diamond(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<Diamond>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws a diamond.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static Diamond Diamond(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<Diamond>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws a diamond.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static Diamond Diamond(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<Diamond>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws a diamond.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static Diamond Diamond(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<Diamond>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		// dot
		/// <summary>
		/// Draws a dot.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static Dot Dot(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush)
			=> ChartMarkerCore<Dot>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws a dot.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static Dot Dot(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<Dot>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws a dot.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static Dot Dot(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<Dot>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws a dot.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static Dot Dot(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<Dot>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws a dot.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static Dot Dot(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<Dot>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws a dot.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static Dot Dot(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<Dot>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);

		// square
		/// <summary>
		/// Draws a square.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static Square Square(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush)
			=> ChartMarkerCore<Square>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws a square.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static Square Square(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<Square>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws a square.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static Square Square(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<Square>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws a square.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static Square Square(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<Square>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws a square.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static Square Square(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<Square>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws a square.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static Square Square(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<Square>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);

		// triangle down
		/// <summary>
		/// Draws a triangle pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static TriangleDown TriangleDown(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush)
			=> ChartMarkerCore<TriangleDown>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws a triangle pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static TriangleDown TriangleDown(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<TriangleDown>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws a triangle pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static TriangleDown TriangleDown(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<TriangleDown>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws a triangle pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static TriangleDown TriangleDown(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<TriangleDown>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws a triangle pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TriangleDown TriangleDown(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<TriangleDown>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws a triangle pointing down.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TriangleDown TriangleDown(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<TriangleDown>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);

		// triangle up
		/// <summary>
		/// Draws a triangle pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static TriangleUp TriangleUp(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush)
			=> ChartMarkerCore<TriangleUp>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null);

		/// <summary>
		/// Draws a triangle pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static TriangleUp TriangleUp(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush)
			=> ChartMarkerCore<TriangleUp>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null);

		/// <summary>
		/// Draws a triangle pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static TriangleUp TriangleUp(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<TriangleUp>(owner, tag, isAutoScale, int.MinValue, time, y, brush, false, null));

		/// <summary>
		/// Draws a triangle pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static TriangleUp TriangleUp(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, Brush brush, bool drawOnPricePanel)
			=> DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () => ChartMarkerCore<TriangleUp>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, brush, false, null));

		/// <summary>
		/// Draws a triangle pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="time"> The time the object will be drawn at.</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TriangleUp TriangleUp(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime time, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<TriangleUp>(owner, tag, isAutoScale, int.MinValue, time, y, null, isGlobal, templateName);

		/// <summary>
		/// Draws a triangle pointing up.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="barsAgo">The bar the object will be drawn at. A value of 10 would be 10 bars ago</param>
		/// <param name="y">The y value or Price for the object</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TriangleUp TriangleUp(NinjaScriptBase owner, string tag, bool isAutoScale, int barsAgo, double y, bool isGlobal, string templateName)
			=> ChartMarkerCore<TriangleUp>(owner, tag, isAutoScale, barsAgo, Core.Globals.MinDate, y, null, isGlobal, templateName);
	}
}
