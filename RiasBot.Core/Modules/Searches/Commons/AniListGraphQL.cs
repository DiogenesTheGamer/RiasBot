using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RiasBot.Modules.Searches.Commons
{
    public class AniListGraphQL
    {
        private const string Url = "https://graphql.anilist.co";
        public JObject Data;

        public async Task<AniListGraphQL> QueryAsync(string query, object variables)
        {
            var graphQLQuery = new GraphQLQuery
            {
                Query = query,
                Variables = variables
            };

            var queryJson = JsonConvert.SerializeObject(graphQLQuery);

            using (var http = new HttpClient())
            {
                var response = await http.PostAsync(Url, new StringContent(queryJson, Encoding.UTF8,"application/json"));
                if (response.IsSuccessStatusCode)
                {
                    JObject data = null;
                    try
                    {
                        data = JObject.Parse(await response.Content.ReadAsStringAsync());
                    }
                    catch
                    {
                        //ignored
                    }
                    var result = new AniListGraphQL
                    {
                        Data = data
                    };
                    
                    return result;
                }

                //TODO: Add log for AniList http post error messages
            }

            return new AniListGraphQL();
        }

        public T GetData<T>(string propertyName)
        {
            return (Data != null) ? JsonConvert.DeserializeObject<T>(Data["data"][propertyName].ToString()) : default;
        }
        
        public T GetData<T>(string propertyName1, string propertyName2)
        {
            return (Data != null) ? JsonConvert.DeserializeObject<T>(Data["data"][propertyName1][propertyName2].ToString()) : default;
        }

        private class GraphQLQuery
        {
            [JsonProperty("query")]
            public string Query { get; set; }
            [JsonProperty("variables")]
            public object Variables { get; set; }
        }
    }
}