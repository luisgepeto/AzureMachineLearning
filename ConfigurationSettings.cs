namespace AzureMachineLearning
{
    public static class ConfigurationSettings 
    {
        public const string InputFilePath= @".\data\german.input.csv";
        public const string ApiKey = "";
        public const string BaseUrl = "https://ussouthcentral.services.azureml.net/workspaces/39eb5872613d4ccc958e059087f2ddc2/services/605936757fbe4ce4ae37eb8c799b2b74";
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