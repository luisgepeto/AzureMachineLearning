using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureMachineLearning
{   

    public static class BatchExecutionApi
    {
        public static string StorageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={BatchExecutionApiSettings.StorageAccountName};AccountKey={ BatchExecutionApiSettings.StorageAccountKey}";        
        public static void GetBatchPrediction()
        {
            InvokeBatchExecutionService().Wait();
        }
        
        static async Task InvokeBatchExecutionService()
        {            
            UploadFileToBlob();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ConfigurationSettings.ApiKey);
                var request = new 
                {
                    Inputs = new
                    {
                        Input1 = new AzureBlobDataReference()
                        {
                            ConnectionString = StorageConnectionString,
                            RelativeLocation = $"{BatchExecutionApiSettings.StorageContainerName}/{Path.GetFileName(ConfigurationSettings.InputFilePath)}"
                        }
                    },
                    Outputs = new
                    {
                        output1 = new AzureBlobDataReference()
                        {
                            ConnectionString = StorageConnectionString,
                            RelativeLocation = $"{BatchExecutionApiSettings.StorageContainerName}/{Path.GetFileName(BatchExecutionApiSettings.OutputFilePath)}"
                        }
                    }                    
                };                

                HttpResponseMessage response = new HttpResponseMessage();
                try
                {
                    Console.WriteLine("Submitting the job...");
                    response = await client.PostAsJsonAsync(ConfigurationSettings.BaseUrl + "/jobs" + "?api-version=2.0", request);
                    response.EnsureSuccessStatusCode();
                    var jobId = await response.Content.ReadAsAsync<string>();
                    Console.WriteLine(string.Format("Job ID: {0}", jobId));
                    Console.WriteLine("Starting the job...");
                    response = await client.PostAsync(ConfigurationSettings.BaseUrl + "/jobs" + "/" + jobId + "/start?api-version=2.0", null);
                    response.EnsureSuccessStatusCode();
                    var jobLocation = ConfigurationSettings.BaseUrl + "/jobs" + "/" + jobId + "?api-version=2.0";
                    var watch = Stopwatch.StartNew();
                    var done = false;
                    while (!done)
                    {
                        Console.WriteLine("Checking the job status...");
                        response = await client.GetAsync(jobLocation);
                        response.EnsureSuccessStatusCode();

                        var status = await response.Content.ReadAsAsync<BatchScoreStatus>();
                        if (watch.ElapsedMilliseconds > BatchExecutionApiSettings.TimeOutInMilliseconds)
                        {
                            done = true;
                            Console.WriteLine(string.Format("Timed out. Deleting job {0} ...", jobId));
                            await client.DeleteAsync(jobLocation);
                        }
                        switch (status.StatusCode)
                        {
                            case BatchScoreStatusCode.NotStarted:
                                Console.WriteLine(string.Format("Job {0} not yet started...", jobId));
                                break;
                            case BatchScoreStatusCode.Running:
                                Console.WriteLine(string.Format("Job {0} running...", jobId));
                                break;
                            case BatchScoreStatusCode.Failed:
                                Console.WriteLine(string.Format("Job {0} failed!", jobId));
                                Console.WriteLine(string.Format("Error details: {0}", status.Details));
                                done = true;
                                break;
                            case BatchScoreStatusCode.Cancelled:
                                Console.WriteLine(string.Format("Job {0} cancelled!", jobId));
                                done = true;
                                break;
                            case BatchScoreStatusCode.Finished:
                                done = true;
                                Console.WriteLine(string.Format("Job {0} finished!", jobId));
                                SaveBlobToFile(status);                                
                                break;
                        }
                        if (!done)
                        {
                            Thread.Sleep(1000);
                        }
                    }
                }
                catch(Exception)
                {
                    await WriteFailedResponse(response);
                }   
            }            
        }

        private static void SaveBlobToFile(BatchScoreStatus status)
        {
            var blobLocation = status.Results.ElementAt(0).Value;
            var resultsLabel = status.Results.ElementAt(0).Key;

            var credentials = new StorageCredentials(blobLocation.SasBlobToken);
            var blobUrl = new Uri(new Uri(blobLocation.BaseLocation), blobLocation.RelativeLocation);
            var cloudBlob = new CloudBlockBlob(blobUrl, credentials);
            cloudBlob.DownloadToFileAsync(BatchExecutionApiSettings.OutputFilePath, FileMode.Create).Wait();
            CleanDuplicateCsvRows();
        }

        static void UploadFileToBlob()
        {
            var blobClient = CloudStorageAccount.Parse(StorageConnectionString).CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(BatchExecutionApiSettings.StorageContainerName);
            container.CreateIfNotExistsAsync().Wait();
            var blob = container.GetBlockBlobReference(Path.GetFileName(ConfigurationSettings.InputFilePath));
            blob.UploadFromFileAsync(ConfigurationSettings.InputFilePath).Wait();
        }
        private static async Task WriteFailedResponse(HttpResponseMessage response)
        {
            Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));
            Console.WriteLine(response.Headers.ToString());
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseContent);
        }
        private static void CleanDuplicateCsvRows()
        {
            var lines = File.ReadAllLines(BatchExecutionApiSettings.OutputFilePath).ToList();
            var uniqueLines = lines.Distinct();
            File.WriteAllText(BatchExecutionApiSettings.OutputFilePath, string.Empty);
            using(var tw = new StreamWriter(BatchExecutionApiSettings.OutputFilePath))
            {
                foreach (var line in uniqueLines)
                    tw.WriteLine(line);
            }
        }
    }
    public class AzureBlobDataReference
    {
        public string ConnectionString { get; set; }
        public string RelativeLocation { get; set; }
        public string BaseLocation { get; set; }
        public string SasBlobToken { get; set; }
    }

    public enum BatchScoreStatusCode
    {
        NotStarted,
        Running,
        Failed,
        Cancelled,
        Finished
    }

    public class BatchScoreStatus
    {
        public BatchScoreStatusCode StatusCode { get; set; }
        public IDictionary<string, AzureBlobDataReference> Results { get; set; }
        public string Details { get; set; }
    }    
}

