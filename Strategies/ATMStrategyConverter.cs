using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace NinjaTrader.Custom.Strategies
{
    public class ATMStrategyConverter : TypeConverter
    {
        public ATMStrategyConverter() 
        {
            // 
            if (Directory.Exists(FolderName))
            {
                // Get all file names in the folder
                atmStrategies = Directory.GetFiles(FolderName).Select(c => c.Split('\\').Last().Replace(".xml", "")).ToArray();
            }
        }
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

        private readonly string FolderName = StrategiesUtilities.ATMFolderName;

        string[] atmStrategies;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {            
            return new StandardValuesCollection(atmStrategies);
        }
    }
}
