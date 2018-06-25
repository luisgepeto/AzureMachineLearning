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
        public static void GetBatchPrediction()
        {
            InvokeBatchExecutionService().Wait();
        }

        static async Task InvokeBatchExecutionService()
        {
            const string BaseUrl = "https://ussouthcentral.services.azureml.net/workspaces/39eb5872613d4ccc958e059087f2ddc2/services/605936757fbe4ce4ae37eb8c799b2b74/jobs";

            const string StorageAccountName = "ipsmachinelearning";
            const string StorageAccountKey = "";
            const string StorageContainerName = "ipsmachinelearningcontainer";
            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", StorageAccountName, StorageAccountKey);
            const string apiKey = "";

            const int TimeOutInMilliseconds = 120 * 1000;

            UploadFileToBlob(@".\data\german.input.csv",StorageContainerName, storageConnectionString);

            using (HttpClient client = new HttpClient())
            {
                var request = new 
                {
                    Inputs = new
                    {
                        Input1 = new AzureBlobDataReference()
                        {
                            ConnectionString = storageConnectionString,
                            RelativeLocation = string.Format("{0}/german.input.csv", StorageContainerName)
                        }
                    },
                    Outputs = new
                    {
                        output1 = new AzureBlobDataReference()
                        {
                            ConnectionString = storageConnectionString,
                            RelativeLocation = string.Format("/{0}/german.output.csv", StorageContainerName)
                        }
                    }                    
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                Console.WriteLine("Submitting the job...");
                var response = await client.PostAsJsonAsync(BaseUrl + "?api-version=2.0", request);
                if (!response.IsSuccessStatusCode)
                {
                    await WriteFailedResponse(response);
                    return;
                }

                string jobId = await response.Content.ReadAsAsync<string>();
                Console.WriteLine(string.Format("Job ID: {0}", jobId));


                Console.WriteLine("Starting the job...");
                response = await client.PostAsync(BaseUrl + "/" + jobId + "/start?api-version=2.0", null);
                if (!response.IsSuccessStatusCode)
                {
                    await WriteFailedResponse(response);
                    return;
                }

                string jobLocation = BaseUrl + "/" + jobId + "?api-version=2.0";
                Stopwatch watch = Stopwatch.StartNew();
                bool done = false;
                while (!done)
                {
                    Console.WriteLine("Checking the job status...");
                    response = await client.GetAsync(jobLocation);
                    if (!response.IsSuccessStatusCode)
                    {
                        await WriteFailedResponse(response);
                        return;
                    }

                    BatchScoreStatus status = await response.Content.ReadAsAsync<BatchScoreStatus>();
                    if (watch.ElapsedMilliseconds > TimeOutInMilliseconds)
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
        }

        private static void SaveBlobToFile(BatchScoreStatus status)
        {
            var blobLocation = status.Results.ElementAt(0).Value;
            var resultsLabel = status.Results.ElementAt(0).Key;

            const string OutputFileLocation = @".\data\german.output.csv";
            var credentials = new StorageCredentials(blobLocation.SasBlobToken);
            var blobUrl = new Uri(new Uri(blobLocation.BaseLocation), blobLocation.RelativeLocation);
            var cloudBlob = new CloudBlockBlob(blobUrl, credentials);
            cloudBlob.DownloadToFileAsync(OutputFileLocation, FileMode.Create).Wait();
        }

        static void UploadFileToBlob(string inputFileLocation, string storageContainerName, string storageConnectionString)
        {
            var blobClient = CloudStorageAccount.Parse(storageConnectionString).CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(storageContainerName);
            container.CreateIfNotExistsAsync().Wait();
            var blob = container.GetBlockBlobReference(Path.GetFileName(inputFileLocation));
            blob.UploadFromFileAsync(inputFileLocation).Wait();
        }
        private static async Task WriteFailedResponse(HttpResponseMessage response)
        {
            Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));
            Console.WriteLine(response.Headers.ToString());
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseContent);
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

