using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CsvHelper;
using Newtonsoft.Json;

namespace AzureMachineLearning
{
    public class Detail
    {
        public List<string> ColumnNames { get; set; }
        public List<List<string>> Values { get; set; }
    }

    public static class RequestResponseApi
    {
        public static string apiKey = "";
        public static string baseUri = "https://ussouthcentral.services.azureml.net/workspaces/39eb5872613d4ccc958e059087f2ddc2/services/605936757fbe4ce4ae37eb8c799b2b74/execute?api-version=2.0&details=true";
        public static Dictionary<string, string> CreditRiskDictionary = new Dictionary<string, string>() { { "1", "Good Credit" }, { "2", "Bad Credit" } };
        public static void GetMultiplePrediction(int numberOfPredictions)
        {
            var csv = new CsvReader(File.OpenText(@".\data\german.input.csv"));                
            var recordCount = 0;
            csv.Read();
            while (csv.Read() && recordCount < numberOfPredictions)
            {
                var answers = new List<string>();
                for(int i =0; i < 20; i++)
                {
                    answers.Add(csv.GetField(i));                        
                }
                recordCount++;
                var answeredQuestions = QuestionsHelper.GetQuestions(answers);
                var result = InvokeRequestResponseService(answeredQuestions).Result;
                Console.WriteLine("Actual credit risk classification: {0}", CreditRiskDictionary[csv.GetField(20)]);
                Console.WriteLine("Predicted credit risk classification: {0}", CreditRiskDictionary[result.Item1]);
                Console.WriteLine("Probability of risk: {0}", result.Item2);
            }  
        }
        public static void GetSinglePrediction()
        {
            var result = InvokeRequestResponseService(AnswerQuestions()).Result;
            Console.WriteLine("Predicted credit risk classification: {0}", CreditRiskDictionary[result.Item1]);
            Console.WriteLine("Probability of risk: {0}", result.Item2);
        }
        private static async Task<(string, string)> InvokeRequestResponseService(List<Question> answeredQuestions)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(baseUri);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey); var columnNames = answeredQuestions.Select(a => a.Column).ToList();
                columnNames.Add("credit_risk");
                var goodCreditValues = answeredQuestions.Select(a => a.CategoryOptions == null ? a.AnswerIndex.ToString() : a.CategoryOptions.ElementAt(a.AnswerIndex - 1).Value).ToList();
                goodCreditValues.Add("1");
                var scoreRequest = new
                {
                    Inputs = new
                    {
                        Input1 = new Detail()
                        {
                            ColumnNames = columnNames,
                            Values = new List<List<string>>(){
                                goodCreditValues
                            }
                        }
                    }
                };
                var response = await client.PostAsJsonAsync("", scoreRequest);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var definition = new { Results = new { Output1 = new { Value = new Detail() } } };
                    var parsedResponse = JsonConvert.DeserializeAnonymousType(result, definition);
                    var scoredLabelsIndex = parsedResponse.Results.Output1.Value.ColumnNames.IndexOf("Scored Labels");
                    var scoredProbabilitiesIndex = parsedResponse.Results.Output1.Value.ColumnNames.IndexOf("Scored Probabilities");
                    var scoredLabelResult = parsedResponse.Results.Output1.Value.Values.ElementAt(0).ElementAt(scoredLabelsIndex);
                    var scoredProbabilityResult = parsedResponse.Results.Output1.Value.Values.ElementAt(0).ElementAt(scoredProbabilitiesIndex);

                    return (scoredLabelResult, (double.Parse(scoredProbabilityResult) * 100).ToString("0.##"));                    
                }
                else
                {
                    Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));
                    Console.WriteLine(response.Headers.ToString());
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseContent);
                    return default((string, string));
                }
            }
        }

        private static List<Question> AnswerQuestions()
        {
            var questions = QuestionsHelper.GetQuestions();
            foreach (var question in questions)
            {
                Console.WriteLine($"{question.Name}:");
                if (question.CategoryOptions != null)
                {
                    for (var i = 1; i <= question.CategoryOptions.Count; i++)
                    {
                        Console.WriteLine($"\t{i}: {question.CategoryOptions.ElementAt(i - 1).Name} ");
                    }
                }
                Console.Write("Answer: ");                
                question.AnswerIndex = int.Parse(Console.ReadLine());
            }
            return questions;
        }
    }
}
