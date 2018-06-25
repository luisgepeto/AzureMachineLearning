namespace AzureMachineLearning
{
    public static class ConfigurationSettings 
    {
        public const string InputFilePath= @".\data\german.input.csv";
        public const string ApiKey = "";
        public const string BaseUrl = "https://ussouthcentral.services.azureml.net/workspaces/39eb5872613d4ccc958e059087f2ddc2/services/ac5f066fd0b74a10aec68b54d8fc59fe";
    }
    public static class BatchExecutionApiSettings
    {
        public const string StorageAccountName = "ipsmachinelearning";
        public const string StorageAccountKey = "";
        public const string StorageContainerName = "ipsmachinelearningcontainer";
        public const int TimeOutInMilliseconds = 120 * 1000;
        public const string OutputFilePath = @".\data\german.output.csv";        
    }
}