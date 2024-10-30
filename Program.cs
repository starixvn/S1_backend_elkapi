using Nest;
using System.Text;
using Newtonsoft.Json;

class Program
{

    public class param
    {
        public string account_id { get; set; }
        public string transaction_id { get; set; }
        public string round_id { get; set; }
    }

    static async Task Main(string[] args)
    {
        string part = "1";
        string apiSuccessFilePath = $"apiSuccess_{part}.txt";
        string apiFailFilePath = $"apiFail_{part}.txt";
        string apiPlayerFilePath = $"apiPlayer_{part}.txt";
        string apiRoundFilePath = $"apiRound_{part}.txt";
        string csvFilePath = $"dragongaming_req_sort_{part}.csv";

        List<string> _playerList = new();
        List<string> _parentBetIdList = new();

        // Read CSV file
        var requestIdList = ReadTextFile(csvFilePath);

        // Initialize HttpClient
        using (HttpClient client = new HttpClient())
        {
            foreach (var requestId in requestIdList)
            {
                //取回elk REQUEST log
                var result = Query(requestId);

                //取得api url
                var urlStart = result.IndexOf("REQUEST URL: ");
                var urlEnd = result.IndexOf(", Method:");
                var url = result.Substring(urlStart + 13, urlEnd - urlStart - 13);
                if (url == "http://tp.nova88.net/DragonGaming/debit" || url == "https://tp.nova88.net/DragonGaming/debit")
                {
                    url = $"{url}/postman";
                }
                //取得request內容
                var bodyStart = result.IndexOf("REQUEST BODY: ");
                var body = result.Substring(bodyStart + 14);

                var pm = JsonConvert.DeserializeObject<param>(body);
                _playerList.Add(pm.account_id);
                _parentBetIdList.Add(pm.round_id);

                // 發送請求             
                var response = await client.PostAsync(url, new StringContent(body));

                var log = new StringBuilder($"{requestId}:");
                if (response.IsSuccessStatusCode)
                {
                    string apiResult = await response.Content.ReadAsStringAsync();
                    log.Append(apiResult);
                    Console.WriteLine("Success: " + log);
                    _ = AppendResultsToFile(apiSuccessFilePath, log.ToString());
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    log.Append(response.StatusCode);
                    _ = AppendResultsToFile(apiFailFilePath, log.ToString());
                }
                Thread.Sleep(500);
            }

            // 會員
            _ = AppendResultsToFile(apiPlayerFilePath, JsonConvert.SerializeObject(_playerList.Distinct()));
            // 局
            _ = AppendResultsToFile(apiRoundFilePath, JsonConvert.SerializeObject(_parentBetIdList.Distinct()));
        }
    }

    private static List<string> ReadTextFile(string filePath)
    {
        var requestIds = new List<string>();
        foreach (var line in File.ReadLines(filePath))
        {
            var sp = line.Split(',');
            requestIds.Add(sp[1]);
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
                .Index("prod-seamless-*")
                .Query(q => q
                    .Bool(b => b
                        .Must(

                       q => q.Term(mt => mt
                                .Field("Properties.RequestId.keyword")
                                .Value(requestId)),
                              q => q.DateRange(dr => dr
                                .Field("Timestamp")
                                .GreaterThanOrEquals(DateTime.Now.AddDays(-30))
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


    public class KeyValue
    {
        public string Key { get; set; }
        public List<string> Value { get; set; }
    }


}
