using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NinjaTrader.Custom.Strategies
{
    public class GeneralUtilities
    {
        const string NewsFilePath = @"\\JOYFUL\TradingFolder\WeekNewsTime.txt";
        /// <summary>
        /// Đọc thông tin về ngày giờ có news từ file 
        /// </summary>
        /// <returns></returns>
        public static string ReadNewsInfoFromFile(Action<string> action)
        {
            var filePath = NewsFilePath;

            try
            {
                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();

                    var data = serializer.Deserialize<NewsTimeReader>(jsonContent);

                    var today = DateTime.Today.DayOfWeek;

                    var newsTime = string.Empty;

                    switch (today)
                    {
                        case DayOfWeek.Sunday:
                            return data.Sunday;
                        case DayOfWeek.Monday:
                            return data.Monday;
                        case DayOfWeek.Tuesday:
                            return data.Tuesday;
                        case DayOfWeek.Wednesday:
                            return data.Wednesday;
                        case DayOfWeek.Thursday:
                            return data.Thursday;
                        case DayOfWeek.Friday:
                            return data.Friday;
                            // No Saturday, ok? 
                    }
                }
            }
            catch (Exception ex)
            {
                action(ex.Message);
            }
            return string.Empty;
        }
    }
}
