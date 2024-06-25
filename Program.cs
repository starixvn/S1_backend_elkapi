using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Nest;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        string csvFilePath = "pg _test.csv";
        string outputFilePath = "output.txt";

        // Read CSV file
        var requestIdList = ReadTextFile(csvFilePath);

        // Initialize HttpClient
        using (HttpClient client = new HttpClient())
        {
            foreach (var requestId in requestIdList)
            {
                var result = Query(requestId);

                // Process the response
                Console.WriteLine($"RequestId: {requestId}, Response: {result}");
                AppendResultsToFile(outputFilePath, result);
            }
        }
    }

    private static List<string> ReadTextFile(string filePath)
    {
        var requestIds = new List<string>();
        foreach (var line in File.ReadLines(filePath))
        {
            var sp = line.Split(',');
            requestIds.Add(sp[3]);
        }
        return requestIds;
    }

    private static string Query(string requestId)
    {
        var elasticClient = GetElasticClient();
        var response = elasticClient.Search<dynamic>(a => a
                .From(0)
                .Size(10)
                .Sort(ss => ss.Descending("Timestamp"))
                .Index("prod-seamless-2024.26")
                .Query(q => q
                    .Bool(b => b
                        .Must(

                       q => q.Term(mt => mt
                                .Field("Properties.RequestId.keyword")
                                .Value(requestId)),
                              q => q.DateRange(dr => dr
                                .Field("Timestamp")
                                .GreaterThanOrEquals(DateTime.Now.AddDays(-1))
                                .LessThanOrEquals(DateTime.Now)),
                              q => q.Term(t => t
                                .Field("Properties.SourceContext.keyword")
                                .Value("RequestLog"))
                              )

                        )
                    ));

        if (response.IsValid)
        {
            var sb = new StringBuilder();
            foreach (var item in response.Documents)
            {
                sb.AppendLine(item["MessageTemplate"]);
            }
            return sb.ToString();
        }
        return $"query error";
    }

    private static ElasticClient GetElasticClient()
    {
        var connectionSetting = new ConnectionSettings(new Uri("http://54.151.226.243:9200"))
            .BasicAuthentication("elastic", "#WpF&@$h2J8Y6LH5");
        return new ElasticClient(connectionSetting);
    }

    private static async Task AppendResultsToFile(string filePath, string result)
    {
        using (StreamWriter writer = File.AppendText(filePath))
        {
             await writer.WriteLineAsync(result);            
        }
    }

}
