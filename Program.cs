using System.Linq;

namespace AzureMachineLearning
{
    public class Program
    {
        public static void Main(string[] args)
        {        
            if (args.ElementAtOrDefault(0) == "batch")
            {
                BatchExecutionApi.GetBatchPrediction();
            }
            else if (args.ElementAtOrDefault(0) == "csv")
            {
                if(!int.TryParse(args.ElementAtOrDefault(1), out var numberOfRecords)) numberOfRecords = 1000;
                RequestResponseApi.GetMultiplePrediction(numberOfRecords);
            }
            else {
                RequestResponseApi.GetSinglePrediction();
            }
        }
    }        
}
