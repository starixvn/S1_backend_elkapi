using Nest;
using System.Text;
using Newtonsoft.Json;

class Program
{

    static async Task Main(string[] args)
    {
        string path = $"affcode.csv";
        //string csvFilePath = $"pg _test.csv";
        string user = $"user.txt";


        // Read CSV file
        var requestIdList = ReadTextFile(path);
        var dic = new Dictionary<string, string>();
        // Initialize HttpClient
        using (HttpClient client = new HttpClient())
        {
            AppendResultsToFile(user, $"total:{requestIdList.Count}");
            int cnt = 0;
            foreach (var requestId in requestIdList)
            {
                cnt++;
                //取回elk REQUEST log
                var result = Query(requestId);



                //取得request內容
                var bodyStart = result.IndexOf("REQUEST BODY: ");
                var body = result.Substring(bodyStart + 14);
                var singUP = JsonConvert.DeserializeObject<SingUP>(body);
                if (dic.TryAdd($"{singUP.userName}-{singUP.affCode}", $"{singUP.userName},{singUP.affCode}"))
                {
                    Console.WriteLine($"{cnt} Success: " + $"{singUP.userName},{singUP.affCode}");
                }                 
              
            }



            foreach (var item in dic)
            {
                AppendResultsToFile(user, item.Value);
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

    private static string Query(string requestId)
    {
        var elasticClient = GetElasticClient();
        var response = elasticClient.Search<dynamic>(a => a
                .From(0)
                .Size(100)
                .Sort(ss => ss.Descending("Timestamp"))
                .Index("prod-membersite-*")
                .Query(q => q
                    .Bool(b => b
                        .Must(

                       q => q.Term(mt => mt
                                .Field("Properties.RequestId.keyword")
                                .Value(requestId)),
                              q => q.DateRange(dr => dr
                                .Field("Timestamp")
                                .GreaterThanOrEquals(DateTime.Now.AddDays(-3))
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


    public class SingUP
    {
        public string affCode { get; set; }
        public object affId { get; set; }
        public string areaCode { get; set; }
        public int currencyId { get; set; }
        public string phoneNo { get; set; }
        public string realName { get; set; }
        public string referralUrl { get; set; }
        public string userLang { get; set; }
        public string userName { get; set; }
        public string userpwd { get; set; }
        public string blackbox { get; set; }
    }



}
