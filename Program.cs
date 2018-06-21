using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CallRequestResponseService
{
    public class Program
    {
        public static string apiKey = "yourapikeyhere";
        public static string baseUri = "yoururihere";
        public static Dictionary<string, string> CreditRiskDictionary = new Dictionary<string, string>(){{"1", "Good Credit"},{"2", "Bad Credit"}};
        public static void Main(string[] args)
        {
            var answers = AnswerQuestions();
            InvokeRequestResponseService(answers).Wait();
        }

        private static async Task InvokeRequestResponseService(List<Attribute> attributes)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(baseUri);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);             var columnNames = attributes.Select(a => a.Column).ToList();
                columnNames.Add("credit_risk");
                var goodCreditValues = attributes.Select(a => a.CategoryOptions == null ? a.AnswerIndex.ToString() : a.CategoryOptions.ElementAt(a.AnswerIndex-1).Value).ToList();                
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
                    var definition = new { Results = new { Output1 = new { Value = new Detail()}}};
                    var parsedResponse = JsonConvert.DeserializeAnonymousType(result, definition);
                    var scoredLabelsIndex = parsedResponse.Results.Output1.Value.ColumnNames.IndexOf("Scored Labels");
                    var scoredProbabilitiesIndex = parsedResponse.Results.Output1.Value.ColumnNames.IndexOf("Scored Probabilities");
                    var scoredLabelResult = parsedResponse.Results.Output1.Value.Values.ElementAt(0).ElementAt(scoredLabelsIndex);
                    var scoredProbabilityResult = parsedResponse.Results.Output1.Value.Values.ElementAt(0).ElementAt(scoredProbabilitiesIndex);

                    Console.WriteLine("Credit Risk Classification: {0}", CreditRiskDictionary[scoredLabelResult]);
                    Console.WriteLine("Probability of risk: {0}", (double.Parse(scoredProbabilityResult)*100).ToString("0.##"));
                }
                else
                {                    
                    Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));
                    Console.WriteLine(response.Headers.ToString());
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(responseContent);
                }
            }
        }

        private static List<Attribute> AnswerQuestions()
        {
            var attributes = new List<Attribute>(){
                new Attribute("status_checking", "Status of existing checking account"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A11", "(A11) ... < 0 MXN"),
                        new CategoryOption("A12", "(A12) 0 <= ... < 200 MXN"),
                        new CategoryOption("A13", "(A13) ... >= 200 MXN"),
                        new CategoryOption("A14", "(A14) no checking account")
                    }
                },
                new Attribute("loan_duration", "Loan duration in months"),
                new Attribute("credit_history", "Credit history"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A30", "(A30) no credits taken/all credits paid back duly"),
                        new CategoryOption("A31", "(A31) all credits at this bank paid back duly"),
                        new CategoryOption("A32", "(A32) existing credits paid back duly till now"),
                        new CategoryOption("A33", "(A33) delay in paying off in the past"),
                        new CategoryOption("A34", "(A34) critical account/other credits existing (not at this bank)")
                    }
                },
                new Attribute("purpose", "Purpose"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A40", "(A40) car (new)"),
                        new CategoryOption("A41", "(A41) car (used)"),
                        new CategoryOption("A42", "(A42) furniture/equipment"),
                        new CategoryOption("A43", "(A43) radio/television"),
                        new CategoryOption("A44", "(A44) domestic appliances"),
                        new CategoryOption("A45", "(A45) repairs"),
                        new CategoryOption("A46", "(A46) education"),
                        new CategoryOption("A47", "(A47) vacation"),
                        new CategoryOption("A48", "(A48) retraining"),
                        new CategoryOption("A49", "(A49) business"),
                        new CategoryOption("A410", "(A410) others")
                    }
                },
                new Attribute("credit_amount", "Credit amount"),
                new Attribute("savings_amt", "Savings account/bonds"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A61", "(A61) ... < 100 MXN"),
                        new CategoryOption("A62", "(A62) 100 <= ... < 500 MXN"),
                        new CategoryOption("A63", "(A63) 500 <= ... < 1000 MXN"),
                        new CategoryOption("A64", "(A64) ... >= 1000 MXN"),
                        new CategoryOption("A65", "(A65) unkown/no savings account")
                    }
                },
                new Attribute("emp_years", "Present employment since"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A71", "(A71) unemployed"),
                        new CategoryOption("A72", "(A72) ... < 1 year"),
                        new CategoryOption("A73", "(A73) 1 <= ... < 4 years"),
                        new CategoryOption("A74", "(A74) 4 <= ... < 7 years"),
                        new CategoryOption("A75", "(A75) ... >= 7 years"),
                    }
                },
                new Attribute("percent_disp_income", "Installment rate in percentage of disposable income"),
                new Attribute("status_sex", "Personal status and sex"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A91", "(A91) male: divorced/separated"),
                        new CategoryOption("A92", "(A92) female: divorced/separated/single"),
                        new CategoryOption("A93", "(A93) male: single"),
                        new CategoryOption("A94", "(A94) male: married/widowed"),
                        new CategoryOption("A95", "(A95) female: single")
                    }
                },
                new Attribute("other_debts", "Other debtors/guarantors"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A101", "(A101) none"),
                        new CategoryOption("A102", "(A102) co-applicant"),
                        new CategoryOption("A103", "(A103) guarantor")                        
                    }
                },
                new Attribute("residence_since", "Present residence since"),
                new Attribute("property", "Property"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A121", "(A121) real estate"),
                        new CategoryOption("A122", "(A122) if not previous: building society savings agreement/life insurance"),
                        new CategoryOption("A123", "(A123) if not in previous: car or other, not in savings account/bonds"),
                        new CategoryOption("A124", "(A124) unknown/no property"),
                    }
                },
                new Attribute("age", "Age in years"),
                new Attribute("other_installments", "Other installment plans"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A141", "(A141) bank"),
                        new CategoryOption("A142", "(A142) stores"),
                        new CategoryOption("A143", "(A143) none")                        
                    }
                },
                new Attribute("housing_status", "Housing"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A151", "(A151) rent"),
                        new CategoryOption("A152", "(A152) own"),
                        new CategoryOption("A153", "(A153) for free")                        
                    }
                },
                new Attribute("num_credits", "Number of existing credits at this bank"),
                new Attribute("job_type", "Job"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A171", "(A171) unemployed/unskilled - non-resident"),                        
                        new CategoryOption("A172", "(A172) unskilled - resident"),                        
                        new CategoryOption("A173", "(A173) skilled employee/official"),                        
                        new CategoryOption("A174", "(A174) management/self-employed/highly qualified employee/officer")
                    }
                },
                new Attribute("num_dependant", "Number of people being liable to provide manintenance for"),
                new Attribute("own_telephone", "Telephone"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A191", "(A191) none"),                                                
                        new CategoryOption("A192", "(A192) yes, registered under the customers name"),                                                
                    }
                },
                new Attribute("foreign_worker", "Foreign worker"){
                    CategoryOptions = new List<CategoryOption>(){
                        new CategoryOption("A201", "(A201) yes"),                                                
                        new CategoryOption("A202", "(A202) no"),                                                
                    }
                }
            };
            foreach (var attribute in attributes)
            {
                Console.WriteLine($"{attribute.Name}:");
                if (attribute.CategoryOptions != null)
                {
                    for (var i = 1; i <= attribute.CategoryOptions.Count; i++)
                    {
                        Console.WriteLine($"\t{i}: {attribute.CategoryOptions.ElementAt(i - 1).Name} ");
                    }
                }
                Console.Write("Answer: ");
                var answer = int.Parse(Console.ReadLine());
                attribute.AnswerIndex = answer;
            }
            return attributes;
        }
        
    }
    
    public class Detail
    {
        public List<string> ColumnNames { get; set; }
        public List<List<string>> Values { get; set; }
    }
    public class Attribute
    {
        public Attribute(string column, string name)
        {
            Column = column;
            Name = name;
        }
        public int AnswerIndex { get; set; }
        public string Name { get; set; }
        public string Column { get; set; }
        public List<CategoryOption> CategoryOptions { get; set; }
    }
    public class CategoryOption
    {
        public CategoryOption(string value, string name)
        {
            Value = value;
            Name = name;
        }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
