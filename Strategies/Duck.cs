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
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using System.IO;
using System.Threading;
using NinjaTrader.Custom.Strategies;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Strategies 
{
    public class Duck : Strategy
    {
	    private int DEMA_Period = 9;	    
	
	    private string atmStrategyId = string.Empty;
	    private string orderId = string.Empty;
	    
	    private double lastDEMA = -1;
		
		private DuckStatus DuckStatus = DuckStatus.Idle;		
		
		/// <summary>
		/// Khoảng cách đảm bảo cho việc giá của stock chạy đúng hướng.
		/// </summary>
	    private double WarranteeFee = 3.0;	

	    /// <summary>
	    /// If loss is more than [MaximumDayLoss], won't trade for that day
	    /// </summary>
	    [NinjaScriptProperty]
		[Display(Name = "Maximum Day Loss ($)", Order = 5, GroupName = "Parameters")]
	    public int MaximumDayLoss { get; set; } = 400;

	    /// <summary>
	    /// If gain is more than [StopWhenGain], won't trade for that day
	    /// </summary>
	    [NinjaScriptProperty]
		[Display(Name = "Stop Trading if Profit is ($)", Order = 6, GroupName = "Parameters")]
	    public int StopGainProfit { get; set; } = 600;
		
		[NinjaScriptProperty]
		[Display(Name = "Allow to move stop gain/loss", Order = 7, GroupName = "Parameters")]
	    public bool AllowToMoveStopLossGain { get; set; } = true;
		
		/// <summary>
		/// Kiếm tra giờ trade(8:00-15:00)
		/// </summary>
		[NinjaScriptProperty]
		[Display(Name = "Check Trading Hour", Order = 8, GroupName = "Parameters")]
	    public bool CheckTradingHour { get; set; } = true;
		
		/*
		[NinjaScriptProperty]
		[Display(Name = "Shift Type (AM/PM/Night)", Order = 9, GroupName = "Parameters")]
		public ShiftType ShiftType { get; set; } = ShiftType.Moning_0700_1500;
		*/
		
		[NinjaScriptProperty]
		[Display(Name = "News Time (Ex: 0900,1300)", Order = 10, GroupName = "Parameters")]
		public string NewsTimeInput { get; set; } = "0830";
		
		/*
		[NinjaScriptProperty]
		[Display(Name = "Ticks to recognize reversal", Description="Khoảng cách (tính bằng ticks) từ B-line đến Bollinger Bands. \r\nMNQ=12, MGC=5", Order = 11, GroupName = "Importants Configurations")]
        public int TicksForWarantee { get; set; } = 12; // 12 ticks ~ 3 điểm MNQ và 1.2 điểm MGC
		*/

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Default ATM Strategy", Description = "Default ATM Strategy", Order = 4, GroupName = "Importants Configurations")]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string FullATMName { get; set; } = "Default_MNQ";

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy", Description = "Strategy sử dụng khi loss/gain more than a half", Order = 4, GroupName = "Importants Configurations")]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfATMName { get; set; } = "Half_MNQ";

        private double PointToMoveGainLoss = 5;
		
		private List<int> NewsTimes = new List<int>();
		
		private OrderAction currentAction = OrderAction.Buy;
		private double filledPrice = -1;
		
		private readonly string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"atmStrategyDuck.txt"); 
		

    	protected override void OnStateChange() 
		{
	      	if (State == State.SetDefaults) 
			{
		        Description = @"Play on 5 minutes frame.";
		        Name = "Duck";
		        Calculate = Calculate.OnPriceChange;
		        EntriesPerDirection = 1;
		        EntryHandling = EntryHandling.AllEntries;
		        IsExitOnSessionCloseStrategy = true;
		        ExitOnSessionCloseSeconds = 30;
		        IsFillLimitOnTouch = false;
		        MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
		        OrderFillResolution = OrderFillResolution.Standard;
		        Slippage = 0;
		        StartBehavior = StartBehavior.WaitUntilFlat;
		        TimeInForce = TimeInForce.Gtc;
		        TraceOrders = false;
		        RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
		        StopTargetHandling = StopTargetHandling.PerEntryExecution;
		        BarsRequiredToTrade = 20;
		        // Disable this property for performance gains in Strategy Analyzer optimizations
		        // See the Help Guide for additional information
		        IsInstantiatedOnEachOptimizationIteration = true;
		        SetOrderQuantity = SetOrderQuantity.DefaultQuantity;
		        DefaultQuantity = 5;
		
		        // Set Properties		        
		        FullATMName = "Default_MNQ";		        
		
		        MaximumDayLoss = 400;
		        StopGainProfit = 600;
				CheckTradingHour = true;
				//ShiftType = ShiftType.Moning_0700_1500;
				NewsTimeInput = "0830";

				//TicksForWarantee = 12;
		    }
			else if (State == State.Configure) 
			{
				ClearOutputWindow();
				AddDataSeries(BarsPeriodType.Minute, 5);
				AddDataSeries(BarsPeriodType.Minute, 1);
				
				CalculatePnL();
				
				try 
				{
					NewsTimes = NewsTimeInput.Split(',').Select(c => int.Parse(c)).ToList();
				}
				catch(Exception e)
				{
					Print(e.Message);
				}

                WarranteeFee = 3.0; //TickSize * TicksForWarantee;

                PointToMoveGainLoss = 5;

                // Load Current 
                if (File.Exists(FileName))
				{
					try
					{
						atmStrategyId = File.ReadAllText(FileName);

                        Print($"WarranteeFee: {WarranteeFee}, PointToMoveGainLoss: {PointToMoveGainLoss}, current atmStrategyId: {atmStrategyId}");
                    }
					catch(Exception e)
					{
						Print(e.Message);
					}
				}
                
            }
	  		else if (State == State.DataLoaded)
			{
				var bollinger1 = Bollinger(1,20); 
				bollinger1.Plots[0].Brush = bollinger1.Plots[2].Brush = Brushes.DarkCyan;
				bollinger1.Plots[1].Brush = Brushes.DeepPink;
				
				var bollinger2 = Bollinger(2,20); 
				bollinger2.Plots[0].Brush = bollinger2.Plots[2].Brush = Brushes.DarkCyan;
				bollinger2.Plots[1].Brush = Brushes.DeepPink;
				
				AddChartIndicator(bollinger1);
				AddChartIndicator(bollinger2);
				AddChartIndicator(DEMA(9));
			}
    	}
		
		private void CalculatePnL()
		{
			try 
			{
				var profitloss = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
				
				Draw.TextFixed(
					    this, 
					    "RunningAcc", 
					    $"Run on: {Account.Name} - Net Liquidation: {Account.Get(AccountItem.NetLiquidation, Currency.UsDollar):C2}", 
					    TextPosition.BottomLeft, 
					    Brushes.DarkBlue,            // Text color
					    new SimpleFont("Arial", 12), // Font and size
					    Brushes.DarkBlue,      // Background color
					    Brushes.Transparent,      // Outline color
					    0                         // Opacity (0 is fully transparent)
					);
			}
			catch(Exception e)
			{
				Print(e.Message);
			}			
		}
	
		private void LocalPrint(object val)
		{
			if (val.GetType() == typeof(string))
			{
				Print($"{DateTime.Now} - [DUCK]-{Time[0]}:: " + val);
			}
			else 
			{
				Print(val);
			}
		}
	    
	    /// <summary>
	    /// Nếu đã đủ lợi nhuận hoặc đã bị thua lỗ quá nhiều thì dừng (bool reachDailyPnL, double totalPnL, bool isWinDay)
	    /// </summary>
	    /// <returns></returns>
	    private bool ReachMaxDayLossOrDayTarget() 
		{      		
			// Calculate today's P&L
			double todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
			
			var reachDayLimit = todaysPnL <= -MaximumDayLoss || todaysPnL >= StopGainProfit;
			
			var additionalText = reachDayLimit ? ". DONE FOR TODAY." : "";
			
			Draw.TextFixed(
					    this, 
					    "PnL", 
					    $"PnL: {todaysPnL:C2}{additionalText}", 
					    TextPosition.BottomRight, 
					    todaysPnL >= 0 ? Brushes.Green : Brushes.Red,            // Text color
					    new SimpleFont("Arial", 12), // Font and size
					    todaysPnL >= 0 ? Brushes.Green : Brushes.Red,      // Background color
					    Brushes.Transparent,      // Outline color
					    0                         // Opacity (0 is fully transparent)
					);
			
			return reachDayLimit;
    	}
		
		private void WriteDistance(string text)
		{
			Draw.TextFixed(
			    this, 
			    "Distance",
                text, 
			    TextPosition.TopRight, 
			    Brushes.DarkBlue,            // Text color
			    new SimpleFont("Arial", 12), // Font and size
			    Brushes.DarkBlue,      // Background color
			    Brushes.Transparent,      // Outline color
			    0                         // Opacity (0 is fully transparent)
			);
		}

        private readonly object lockEnterOrder = new object();
        private void EnterOrder(OrderAction action, State state, DuckStatus status) 
		{
			lock (lockEnterOrder) 
			{

                bool isRealTime = state == State.Realtime;

                var middleOfEMA = (ema29 + ema51) / 2;

                filledPrice = Math.Round(middleOfEMA * 4.0) / 4.0;

                currentAction = action;

                LocalPrint($"Enter order {action} with status {status}");

                if (isRealTime)
                {
                    atmStrategyId = GetAtmStrategyUniqueId();
                    orderId = GetAtmStrategyUniqueId();

					// If profit reaches half of daily goal or lose half of daily loss 
                    var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                    var reacHalf = todaysPnL <= -MaximumDayLoss / 2 || todaysPnL >= StopGainProfit /2;
					var atmStragtegyName = reacHalf ? HalfATMName : FullATMName;

                    try
                    {
                        File.WriteAllText(FileName, atmStrategyId);
                    }
                    catch (Exception e)
                    {
                        LocalPrint(e.Message);
                    }

                    var existingOrders = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

					if (!existingOrders)
					{
                        // Enter a BUY/SELL order current price
                        AtmStrategyCreate(
                            action,
                            status == DuckStatus.OrderExist ? OrderType.Market : OrderType.Limit, // Market price if fill immediately
                            status == DuckStatus.OrderExist ? 0 : filledPrice,
                            0,
                            TimeInForce.Day,
                            orderId,
                            atmStragtegyName,
                            atmStrategyId,
                            (atmCallbackErrorCode, atmCallBackId) =>
                            {
                                if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                                {
                                    LocalPrint($"Enter {action} - New StrategyID: {atmStrategyId} - New status: {DuckStatus}");
                                }
                            });

						/*
						if (status == DuckStatus.OrderExist)
						{
                            Thread.Sleep(1000);
                        }
						*/
                    }
                }
                else
                {
                    /*
                    if (action == OrderAction.Buy) 
                    {
                      EnterLong();
                    }
                    else if (action == OrderAction.Sell) 
                    {
                      EnterShort();
                    }
                    SetStopLoss(CalculationMode.Ticks, StopLossTicks);
                    SetProfitTarget(CalculationMode.Ticks, StopGainTicks);
                    */
                }
            }
		}

        

        bool NearNewsTime(int time, int newsTime)
		{
			// newsTime format: 0700,0830,1300
			var minute = newsTime % 100; 
			var hour = newsTime / 100; 
			
			var left = -1; 
			var right = -1; 
			
			if (minute >= 5 && minute <= 54) // time is 0806 --> no trade from 080100 to 081100
			{
				left = hour * 10000 + (minute - 5) * 100; 
				right = hour * 10000 + (minute + 5) * 100; 			
			}
			else if (minute < 5) // time is 0802 --> no trade from 075700 to 080700
			{
				left = (hour - 1) * 10000 + (minute + 55) * 100; 
				right = hour * 10000 + (minute + 5) * 100;
			}
			else // minute >= 55 - time is 0856 --> No trade from 085100 to 090100
			{
				left = hour * 10000 + (minute - 5) * 100; 
				right = (hour + 1) * 10000 + (minute - 55) * 100;
			}
			
			return left < time && time < right;		
		}
		
	    // Allow trade if: 
		//  	- Choose ShiftType is morning: Allow trade from 7:00am to 3:00pm
		//  	- Choose ShiftType is afternoon: Allow trade from 5:00pm to 11:00pm
		//  	- Choose ShiftType is overnight: Allow trade from 11:00pm to 7:00am
		//      - Avoid news
	    private bool IsTradingHour() 
		{
			if (!CheckTradingHour)
			{
				return true;
			}
			var time = ToTime(Time[0]);

			/*
			
			if (ShiftType == ShiftType.Moning_0700_1500 && (time < 070000 || time > 150000))
			{
				LocalPrint($"Time: {time} - Shift {ShiftType} --> Not trading hour");
				return false; 
			}
			else if (ShiftType == ShiftType.Afternoon_1700_2300 && (time < 170000 || time > 230000))
			{
				LocalPrint($"Time: {time} - Shift {ShiftType} --> Not trading hour");
				return false; 
			} 
			else if (ShiftType == ShiftType.Night_2300_0700 && (time >= 070000 && time <= 230000))
			{
				LocalPrint($"Time: {time} - Shift {ShiftType} --> Not trading hour");
				return false;
			}
			*/
			
			var newTime = NewsTimes.FirstOrDefault(c => NearNewsTime(time, c));
			
			if (newTime != 0)
			{			
				LocalPrint($"News at {newTime} --> Not trading hour");
				return false;
			}
			
			return true;    
		}	
		
		private void MoveStopGainOrLoss(double __currentPrice)
		{
			if (!AllowToMoveStopLossGain)
			{
				LocalPrint("NOT allow to move stop loss or stop gain");
				return;
			}
			else if (string.IsNullOrEmpty(atmStrategyId))
			{
                LocalPrint("Don't have atmStrategyId information");
                return;
            }
			
			try
			{
				var updatedPrice = __currentPrice;

                var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains("Stop")).ToList();
                var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains("Target")).ToList();

                if (targetOrders.Count() != 1 || stopOrders.Count() != 1)
                {
                    return;
                }				

                var targetOrder = targetOrders.FirstOrDefault();
                var stopOrder = stopOrders.FirstOrDefault();

                LocalPrint($"Check to move Stop gain or Loss - Current: Gain: {targetOrder.LimitPrice:F2}, Loss: {stopOrder.StopPrice:F2}, Point: {PointToMoveGainLoss},  Price: {updatedPrice} ");

                if (currentAction == OrderAction.Buy)
                {
                    // Dịch stop gain nếu giá quá gần target
                    if (updatedPrice + PointToMoveGainLoss > targetOrder.LimitPrice)
                    {
                        var newTarget = updatedPrice + PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            newTarget,
                            0,
                            targetOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển TARGET đến {newTarget} - BUY");
                    }

                    // Dịch chuyển stop loss nếu giá quá xa stop loss
                    if (stopOrder.StopPrice > filledPrice && stopOrder.StopPrice + PointToMoveGainLoss < updatedPrice)
                    {
                        var newStop = updatedPrice - PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            0,
                            newStop,
                            stopOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển LOSS đến {newStop} - BUY");
                    }
                }
                else if (currentAction == OrderAction.Sell)
                {
                    // Dịch stop gain nếu giá quá gần target
                    if (updatedPrice - PointToMoveGainLoss < targetOrder.LimitPrice)
                    {
                        var newTarget = updatedPrice - PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            newTarget,
                            0,
                            targetOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển TARGET đến {newTarget} - SELL");
                    }

                    // Dịch chuyển stop loss nếu giá quá xa stop loss
                    if (stopOrder.StopPrice < filledPrice && stopOrder.StopPrice - PointToMoveGainLoss > updatedPrice)
                    {
                        var newStop = updatedPrice + PointToMoveGainLoss;

                        AtmStrategyChangeStopTarget(
                            0,
                            newStop,
                            stopOrder.Name,
                            atmStrategyId);

                        LocalPrint($"Dịch chuyển LOSS đến {newStop} - SELL");
                    }
                }
            }
			catch (Exception e) 
			{
				LocalPrint(e.Message);
			}
		}
	
	    private int count = 0;	
		private readonly object lockOjbject = new Object();	
		
		double ema29 = -1; 
		double ema51 = -1;        
        double ema120 = -1;
        double ema89 = -1;
        double high1m = -1; 
		double low1m = -1;
		double open1m = -1;
		double currentPrice1m = -1; 
		
		double upperBB5m = -1;	
	    double lowerBB5m = -1;	
	    double middleBB5m = -1;	
		
		double upperBB5m_Std2 = -1;
	    double lowerBB5m_Std2 = -1;
		
		double lastUpperBB5m = -1; 
		double lastLowerBB5m = -1; 		
		
		//double lastOpenBar5m = -1;	
	    //double lastCloseBar5m = -1;	
	    //double currentOpenBar5m = -1;	
	    double currentPrice5m = -1;	
		double currentDEMA = -1;

        private DateTime lastExecutionTime = DateTime.MinValue;
        private int executionCount = 0;
		private int NextBarProgress = 1; 
        protected override void OnBarUpdate() 
		{
            if (CurrentBar < DEMA_Period)
            {
                return;
            }

            lock (lockOjbject)
			{
				try 
				{                    
                    if (BarsInProgress == 2) // 1 minutes
			      	{						
						ema29 = EMA(29).Value[0]; 
						ema51 = EMA(51).Value[0];
						ema120 = EMA(120).Value[0];
                        ema89 = EMA(89).Value[0];
                        low1m = Low[0];
						high1m = High[0];
						open1m = Open[0];
						currentPrice1m = Close[0];
                        //LocalPrint($"BarsInProgress = 2 (1M): New prices for 1m: ema29: {ema29:F2}, ema51: {ema51:F2}.");
                    }
			      	else if (BarsInProgress == 1) // 5 minues
			      	{
				        var bollinger = Bollinger(1, 20);
						var bollinger_Std2 = Bollinger(2, 20);
				        
						upperBB5m = bollinger.Upper[0];
				        lowerBB5m = bollinger.Lower[0];
				        middleBB5m = bollinger.Middle[0];
						
						upperBB5m_Std2 = bollinger_Std2.Upper[0];
						lowerBB5m_Std2 = bollinger_Std2.Lower[0];
						
						lastUpperBB5m = bollinger.Upper[1];
				        lastLowerBB5m = bollinger.Lower[1];
				
				        //Open and Close price of 5 minute frame
				        //lastOpenBar5m = Open[1];
				        //lastCloseBar5m = Close[1];
				        //currentOpenBar5m = Open[0];
				        currentPrice5m = Close[0]; // = currentClose
				
				        currentDEMA = DEMA(DEMA_Period).Value[0]; 
						lastDEMA = DEMA(DEMA_Period).Value[1];
                        //LocalPrint($"BarsInProgress = 1 (5M):New prices for 5m: upperBB5m: {upperBB5m:F2}, lowerBB5m: {lowerBB5m:F2}.");
                    }

					if (State != State.Realtime)
					{
						return;
					}
					else if (DateTime.Now.Subtract(lastExecutionTime).TotalSeconds < 1)
					{
						return;
					}
                    lastExecutionTime = DateTime.Now;

                    var isTradingHour = IsTradingHour();

                    executionCount++;
                    LocalPrint($"OnBarUpdate execution Count: {executionCount}, Price: {Close[0]}");                    

                    if (DuckStatus == DuckStatus.Idle) 
					{
						// Kiểm tra xem thực sự KHÔNG có lệnh nào hay không?
						var hasActiveOrder = Account.Orders.Any(order =>order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);
												
						if (hasActiveOrder) // Nếu có lệnh thì đổi lại status
						{
							DuckStatus = DuckStatus.OrderExist;
						}
						else 
						{
							if (!isTradingHour)
							{
								return;
							}
							
							var reachDailyPnL = ReachMaxDayLossOrDayTarget();
									
					        if (reachDailyPnL) 
							{
								LocalPrint($"Reach daily gain/loss. Stop trading.");
								return;
					        }
							
							var enterLong = ShouldTrade(OrderAction.Buy);
							
							var enterShort = enterLong == DuckStatus.Idle ? ShouldTrade(OrderAction.Sell) : DuckStatus.Idle; 
							
							LocalPrint($"I'm waiting - {DuckStatus} - Check to enter LONG: {enterLong}, check to ebter SHORT: {enterShort}");							
							
							if (enterLong == DuckStatus.FillOrderPending || enterLong == DuckStatus.OrderExist)  // Vào lệnh LONG market hoặc set lệnh LONG LIMIT
							{
								DuckStatus = enterLong;
								EnterOrder(OrderAction.Buy, State, enterLong);
								Print("Enter Long");
							}
							else if (enterShort == DuckStatus.FillOrderPending || enterShort == DuckStatus.OrderExist)
							{
								DuckStatus = enterShort;
								EnterOrder(OrderAction.Sell, State, enterShort);
								Print("Enter Short");
							}
							else if (enterLong == DuckStatus.WaitingForGoodPrice)
							{
								DuckStatus = enterLong; 							
								currentAction = OrderAction.Buy; 
								Print("WaitingForGoodPrice to LONG");
							}
							else if (enterShort == DuckStatus.WaitingForGoodPrice)
							{
								DuckStatus = enterShort; 							
								currentAction = OrderAction.Sell; 
								Print("WaitingForGoodPrice to SHORT");
							}							
						}
					}
					else if (DuckStatus == DuckStatus.OrderExist) // Move stop gain/loss
					{
						// Check the order really exist
						var hasActiveOrder = Account.Orders.Any(order =>order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);
							
						if (!hasActiveOrder) // If no order exist --> Có thể vì 1 lý do nào đó (manually close, error, etc.) các lệnh đã bị đóng
						{
							DuckStatus = DuckStatus.Idle;
							LocalPrint($"Chuyển về trạng thái IDLE từ OrderExist");
						}
						else 
						{
							LocalPrint($"{DuckStatus} - Current price: {currentPrice5m}");
							MoveStopGainOrLoss(currentPrice5m);						
						}
					}
					else if (DuckStatus == DuckStatus.FillOrderPending)
					{
						// Move LIMIT order nếu giá di chuyển quá nhiều
						var activeOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);
						
						if (!activeOrders.Any()) // Nếu không có lệnh đợi filled
						{
							DuckStatus = DuckStatus.Idle;
							LocalPrint($"Chuyển về trạng thái IDLE từ FillOrderPending");
						}
						else if (activeOrders.Any(c => c.Name.Contains("Target") || c.Name.Contains("Stop")))
						{
							DuckStatus = DuckStatus.OrderExist;
							LocalPrint($"Chuyển về trạng thái ORDER_EXISTS từ FillOrderPending");
						}
						else // if (activeOrders.Any(c => c.Name.Contains("Entry")))
						{
							UpdatePendingOrder(activeOrders.FirstOrDefault());
						}
					}
					else if (DuckStatus == DuckStatus.WaitingForGoodPrice)
					{
						var hasActiveOrder = Account.Orders.Any(order =>order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);
						
						if (hasActiveOrder) // If no order exist --> Có thể vì 1 lý do nào đó (manually close, error, etc.) các lệnh đã bị đóng
						{
							DuckStatus = DuckStatus.OrderExist;
							LocalPrint($"Chuyển sang trạng thái ORDER_EXISTS từ WaitingForGoodPrice");
						}
						else 
						{
							// Cập nhật luôn trạng thái mới
							var newStatus = WaitForTradeCondition();

                            //LocalPrint($"WaitingForGoodPrice - NewStatus: {newStatus}");
                            var existingOrders = Account.Orders.Any(order => order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted);

							if (existingOrders)
							{
                                DuckStatus = DuckStatus.OrderExist;
                                LocalPrint($"Chuyển sang trạng thái ORDER_EXISTS từ WaitingForGoodPrice");
                            }
							else if (!existingOrders && (newStatus == DuckStatus.OrderExist || newStatus == DuckStatus.FillOrderPending))
							{
								EnterOrder(currentAction, State, newStatus);
							}
							
							DuckStatus = newStatus;
						}
					}
				}
				catch(Exception e)
				{
					LocalPrint(e.Message);
				}
			}
        }
		
		/**
		* Hàm này chỉ làm việc khi DuckStatus là Idle
		*/ 
		

        private DuckStatus ShouldTrade(OrderAction action) 
		{			
			/*
			* Điều kiện vào lệnh: 
			* 1. Điều kiện về B-line (DEMA): lastDEMA ở ngoài và currentDEMA rất gần upper (nếu SELL) hoặc lower (nếu BUY) 
			* 2. Nếu điều kiện 1 được thỏa mãn
			*/
			
			if (DuckStatus != DuckStatus.Idle)  
			{
				return DuckStatus;
			}

			var minDistance = Math.Min(Math.Abs(currentDEMA - lowerBB5m), Math.Abs(upperBB5m - currentDEMA));
            WriteDistance($"Distance: {minDistance:F2}");

			if (action == OrderAction.Buy)
			{
				var foundCross = lastDEMA < lastLowerBB5m && // DEMA ở dưới BB 
					((currentDEMA < lowerBB5m && lowerBB5m - currentDEMA <= WarranteeFee) || currentDEMA >= lowerBB5m);  // DEMA ở dưới BB nhưng cách BB <=3 pts, hoặc DEMA đã >= BB - tức là đã đi vào trong

				if (!foundCross)
				{
					LocalPrint($"NOT found cross BUY, lastDEMA: {lastDEMA:F2} lastUpperBB5m: {lastLowerBB5m:F2}, currentDEMA: {currentDEMA:F2}, lowerBB5m:{lowerBB5m:F2}, WarranteeFee: {WarranteeFee:F2}");
					return DuckStatus.Idle; // Tiếp tục trạng thái hiện tại
				}
				else if (open1m < Math.Max(ema29, ema51)) // Found cross, nhưng Open của nến 1 phút vẫn ở dưới EMA29/51)
				{
					LocalPrint($"Found cross BUY, but open1m {open1m:F2} < Math.Max({ema29:F2}, {ema51:F2})");
					return DuckStatus.WaitingForGoodPrice; // Đợi khi nào có nến 1 phút vượt qua EMA29/51 thì set lệnh
				}
				else if (//open1m >= Math.Max(ema29, ema51) && 
					currentPrice1m < Math.Min(ema89, ema120) && Math.Min(ema89, ema120) - currentPrice1m >= 10)
				{
					LocalPrint("Vào lệnh BUY theo giá MARKET");
					return DuckStatus.OrderExist;
				}
				else if (//open1m > Math.Max(ema29, ema51)
					Math.Max(ema29, ema51) < Math.Min(ema89, ema120) && Math.Min(ema89, ema120) - Math.Max(ema29, ema51) < 10)
				{
					LocalPrint("Chờ fill lệnh BUY");
					return DuckStatus.FillOrderPending;
				}				
      		}
			else if (action == OrderAction.Sell) 
			{
				var foundCross = lastDEMA > lastUpperBB5m && // DEMA ở trên BB 
					((currentDEMA > upperBB5m && currentDEMA - upperBB5m <= WarranteeFee) || currentDEMA <= upperBB5m); // DEMA đã vào trong BB 					

				if (!foundCross)
				{
					LocalPrint($"NOT found cross SELL, lastDEMA: {lastDEMA:F2} lastUpperBB5m: {lastUpperBB5m:F2}, currentDEMA: {currentDEMA:F2}, upperBB5m:{upperBB5m:F2}, WarranteeFee: {WarranteeFee:F2}");
					return DuckStatus.Idle; // Tiếp tục trạng thái hiện tại
				}
				else if (open1m > Math.Min(ema29, ema51)) // foundCross = true, nhưng open của nến 1 phút vẫn nằm trên EMA29/51 (chưa vượt qua được)
				{
					LocalPrint($"Found cross SELL, but open1m {open1m:F2} > Math.Min({ema29:F2}, {ema51:F2})");
					return DuckStatus.WaitingForGoodPrice;
				}
				else if (currentPrice1m > Math.Max(ema89, ema120) && currentPrice1m - Math.Max(ema89, ema120) >= 10)
				{// --> Cho phép vào lệnh
					LocalPrint("Vào lệnh SELL theo giá MARKET");
					return DuckStatus.OrderExist;
				}
				else if (
					Math.Min(ema29, ema51) > Math.Max(ema89, ema120) 
					&& Math.Max(ema89, ema120) - Math.Min(ema29, ema51) < 10)// foundCross = true AND open1m < Math.Min(ema29, ema51) AND Math.Min(ema29, ema51) - open1m  > WarranteeFee
				{ // --> Mở nến đã đi quá xa đường EMA29/51 --> Đợi back test về EMA29/51
					LocalPrint("Chờ fill lệnh SELL");
					return DuckStatus.FillOrderPending;
				}				
			}
			
      		return DuckStatus.Idle;;
    	}


        // Hàm này chỉ làm việc với DuckStatus hiện tại là DuckStatus.WaitingForGoodPrice
        private DuckStatus WaitForTradeCondition()
		{
			if (DuckStatus != DuckStatus.WaitingForGoodPrice)
			{
				return DuckStatus;
			}

            var minDistance = Math.Min(Math.Abs(currentDEMA - lowerBB5m), Math.Abs(upperBB5m - currentDEMA));
            WriteDistance($"Distance: {minDistance:F2}");

            if (currentAction == OrderAction.Buy)
			{
				// Kiểm tra lại cross 
				if (currentDEMA < lowerBB5m && lowerBB5m - currentDEMA >= WarranteeFee + 8 * TickSize) // DEMA vẫn nằm dưới lowerBB5m và ngày càng cách xa lowerBB5m
				{
					LocalPrint($"SETUP hết đẹp, quay trở về trạng thái IDLE");
					return DuckStatus.Idle; // Hết setup đẹp, tiếp tục trở về Idle để đợi
				}				
				else if (open1m < Math.Max(ema29, ema51)) // Found cross, nhưng Open của nến 1 phút vẫn ở dưới EMA29/51)
				{
					LocalPrint($"Continue waiting, open1m {open1m:F2} < Math.Max({ema29:F2}, {ema51:F2})");
					return DuckStatus.WaitingForGoodPrice; // Tiếp tục chờ đợi
				}
                else if (//open1m >= Math.Max(ema29, ema51) && 
                    currentPrice1m < Math.Min(ema89, ema120) && Math.Min(ema89, ema120) - currentPrice1m >= 10)
                {
                    LocalPrint("Vào lệnh BUY theo giá MARKET");
                    return DuckStatus.OrderExist;
                }
                else if (//open1m > Math.Max(ema29, ema51)
                    Math.Max(ema29, ema51) < Math.Min(ema89, ema120) && Math.Min(ema89, ema120) - Math.Max(ema29, ema51) < 10)
                {
                    LocalPrint("Chờ fill lệnh BUY");
                    return DuckStatus.FillOrderPending;
                }
            }
			else if (currentAction == OrderAction.Sell)
			{
				if (currentDEMA > upperBB5m && currentDEMA - upperBB5m >= WarranteeFee + 8 * TickSize) // DEMA vẫn nằm trên upperBB5m và ngày càng cách xa upperBB5m
				{
                    LocalPrint($"SETUP hết đẹp, quay trở về trạng thái IDLE");
                    return DuckStatus.Idle; // Hết setup đẹp, tiếp tục trở về Idle để đợi
				}
                else if (open1m > Math.Min(ema29, ema51)) // foundCross = true, nhưng open của nến 1 phút vẫn nằm trên EMA29/51 (chưa vượt qua được)
                {
                    LocalPrint($"Found cross SELL, but open1m {open1m:F2} > Math.Min({ema29:F2}, {ema51:F2})");
                    return DuckStatus.WaitingForGoodPrice;
                }
                else if (currentPrice1m > Math.Max(ema89, ema120) && currentPrice1m - Math.Max(ema89, ema120) >= 10)
                {// --> Cho phép vào lệnh
                    LocalPrint("Vào lệnh SELL theo giá MARKET");
                    return DuckStatus.OrderExist;
                }
                else if (
                    Math.Min(ema29, ema51) > Math.Max(ema89, ema120)
                    && Math.Max(ema89, ema120) - Math.Min(ema29, ema51) < 10)// foundCross = true AND open1m < Math.Min(ema29, ema51) AND Math.Min(ema29, ema51) - open1m  > WarranteeFee
                { // --> Mở nến đã đi quá xa đường EMA29/51 --> Đợi back test về EMA29/51
                    LocalPrint("Chờ fill lệnh SELL");
                    return DuckStatus.FillOrderPending;
                }
            }

			return DuckStatus.WaitingForGoodPrice;
		}
		
		private void UpdatePendingOrder(Order pendingOrder)
		{
			if (DuckStatus != DuckStatus.FillOrderPending)
			{
				return;
			}
			if (pendingOrder == null) 
			{
				foreach (var order in Account.Orders)
				{
					LocalPrint($"Debug - {order.OrderState}");
				}
                DuckStatus = DuckStatus.Idle;
                LocalPrint($"ERROR: DuckStatus không đúng. Reset status. - Current status: {DuckStatus}");
                return;
            }

			LocalPrint($"UpdatePendingOrder - Order {pendingOrder.OrderAction} - Price: {pendingOrder.LimitPrice} ");			
			
			if (string.IsNullOrEmpty(atmStrategyId) || string.IsNullOrEmpty(orderId)) // We don't have any information
			{
				return;
			}
			
			var cancelByPrice = 
				(currentAction == OrderAction.Buy && currentPrice5m > upperBB5m_Std2) || 
				(currentAction == OrderAction.Sell && currentPrice5m < lowerBB5m_Std2);

			LocalPrint($"currentAction: {currentAction}, currentPriceBar5m: {currentPrice5m:F2}, upperBB5m_Std2: {upperBB5m_Std2:F2}, lowerBB5m_Std2: {lowerBB5m_Std2:F2}");
			
			var isMarketClosed = ToTime(Time[0]) >= 150000; 
			
			if (isMarketClosed || cancelByPrice || (Time[0] - pendingOrder.Time).TotalMinutes > 60) // Cancel lệnh vì market đóng cửa, vì giá đi quá cao hoặc vì
			{
				AtmStrategyCancelEntryOrder(orderId);
				
				orderId = null;
				atmStrategyId = null;
				DuckStatus = DuckStatus.Idle; 
				
				LocalPrint(isMarketClosed ? $"Set Idle do hết giờ trade." : $"Chờ quá lâu, cancel lệnh. Status {DuckStatus} now. ");
			}			
			else 
			{
				var middleOfEMA = (ema29 + ema51) / 2.0; 
				var newPrice = Math.Round(middleOfEMA * 4) / 4.0;

				var shouldMoveBuy = currentAction == OrderAction.Buy 
					&& newPrice > pendingOrder.LimitPrice
					&& newPrice - PointToMoveGainLoss > pendingOrder.LimitPrice;

				var shouldMoveSell = currentAction == OrderAction.Sell 
					&& newPrice < pendingOrder.LimitPrice 
					&& newPrice + PointToMoveGainLoss < pendingOrder.LimitPrice;

				if (shouldMoveBuy || shouldMoveSell)
				{
                    filledPrice = newPrice;
                    var changedSuccessdful = AtmStrategyChangeEntryOrder(newPrice, 0, orderId);
					var text = changedSuccessdful ? "success" : "UNsuccess";

                    LocalPrint($"Update LIMIT {text} {currentAction} price: ${newPrice:F2}");
                }
            }
			/*
			End of UpdatePendingOrder
			*/			
		}

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
			//base.OnPositionUpdate(position, averagePrice, quantity, marketPosition);
			LocalPrint($"OnPositionUpdate: {marketPosition}, {quantity}");
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            //base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);
            LocalPrint($"OnExecutionUpdate: {price}, {orderId} ,IsEntryStrategy: {execution.IsEntryStrategy}, IsEntry:{execution.IsEntry}");
        }

        /*
		protected override void OnOrderUpdate(
			Order order, 
			double limitPrice, 
			double stopPrice, 
			int quantity, 
			int filled, 
			double averageFillPrice, 
			OrderState orderState, 
			DateTime time, 
			ErrorCode error, 
			string comment) 
		{
	   		LocalPrint($"OnOrderUpdate:: Id: {order.Id}, limitPrice: {limitPrice:F2}, stop: {stopPrice:F2}");
			
			CalculatePnL();
	    }
		*/

        /*
		* End of this class
		*/
    }
}