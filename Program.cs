using Nest;
using System.Text;
using Newtonsoft.Json;

class Program
{

    static async Task Main(string[] args)
    {
        string path = $"RequestIdList.csv";
        string user = $"Result.txt";


        // Read CSV file
        var requestIdList = ReadTextFile(path);
        var dic = new Dictionary<string, string>();
        // Initialize HttpClient
        using (HttpClient client = new HttpClient())
        {
            await AppendResultsToFile(user, $"total:{requestIdList.Count}");
            int cnt = 0;
            foreach (var requestId in requestIdList)
            {
                cnt++;
                //取回elk REQUEST log
                var result = await Query(requestId);
                Console.WriteLine(result);



                //取得request內容
                var bodyStart = result.IndexOf("REQUEST BODY: ");
                var body = result.Substring(bodyStart + 14);
                Console.WriteLine($"{cnt} Success: {body}" );
              
            }



            foreach (var item in dic)
            {
                await AppendResultsToFile(user, item.Value);
            }
        }
    }

    private static List<string> ReadTextFile(string filePath)
    {
        var requestIds = new List<string>();
        foreach (var line in File.ReadLines(filePath))
        {
            var sp = line.Split(',');
            requestIds.Add(sp[0]);
        }
        return requestIds;
    }

    private static async Task<string> Query(string requestId)
    {
        var elasticClient = GetElasticClient();
        
        var searchResponse = await elasticClient.SearchAsync<dynamic>(s => s
            .Index(".ds-12bet-prod-k8s_api_seamlesswallet-*")
            .Query(q => q
                .Bool(b => b
                    .Must(
                        m => m.Exists
                        (e => e
                            .Field("MessageTemplate") //  MessageTemplate 欄位
                        ),
                        m => m.DateRange
                        (r => r
                            .Field("@timestamp") // 使用 @timestamp 欄位   
                            .GreaterThanOrEquals("2025-02-05T09:00:00+08:00")
                            .LessThanOrEquals("2025-02-05T10:00:00+08:00")
                        ),
                        m => m.Term
                        (t => t
                            .Field("Properties.SourceContext")
                            .Value("RequestLog")
                        ),
                        m => m.Term
                        (t => t
                            .Field("Properties.RequestId")
                            .Value(requestId)
                        )   
                    )
                )
            )
            .Size(100)
            .Sort(sort => sort.Descending("@timestamp"))
        );

        
        //Console.WriteLine($"查詢條件: {searchResponse.DebugInformation}");

        if (!searchResponse.IsValid)
        {
            // 輸出完整的查詢條件和回應
            Console.WriteLine($"查詢條件: {searchResponse.DebugInformation}");
            Console.WriteLine($"API Url: {searchResponse.ApiCall?.Uri}");
            Console.WriteLine($"Request JSON: {searchResponse.ApiCall?.RequestBodyInBytes}");
            Console.WriteLine($"Response JSON: {searchResponse.ApiCall?.ResponseBodyInBytes}");

            Console.WriteLine($"查詢錯誤: {searchResponse.DebugInformation}");
            Console.WriteLine($"RequestId: {requestId}");
            return "query error";
        }

        if (!searchResponse.Documents.Any())
        {
            Console.WriteLine($"找不到資料 - RequestId: {requestId}");
            Console.WriteLine($"Total hits: {searchResponse.Total}");
            return "no results found";
        }

        var sb = new StringBuilder();
        foreach (var item in searchResponse.Documents)
        {
            try 
            {
                sb.AppendLine(item["MessageTemplate"]);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"error: {ex.Message}");
            }
        }

        return sb.ToString();
    }

    private static ElasticClient GetElasticClient()
    {
        var connectionSetting = new ConnectionSettings(new Uri("https://elasticsearch.12bet-uat.oneotherops.net"))
            .BasicAuthentication("hans.chen", "hans.chen")
            .DisableDirectStreaming()
            .PrettyJson();

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
