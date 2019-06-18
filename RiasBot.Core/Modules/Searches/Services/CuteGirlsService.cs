using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RiasBot.Commons.Attributes;
using RiasBot.Services;

namespace RiasBot.Modules.Searches.Services
{
    [Service]
    public class CuteGirlsService
    {
        private readonly IBotCredentials _creds;
        public CuteGirlsService(IBotCredentials creds)
        {
            _creds = creds;
        }

        public async Task<string> GetNekoImageAsync()
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add("Authorization", "Wolke " + _creds.WeebServicesToken);
                http.DefaultRequestHeaders.Add("User-Agent", "RiasBot/" + RiasBot.Version);
                var response = await http.GetAsync(_creds.WeebApi + "images/random?type=neko");
                if (response.IsSuccessStatusCode)
                {
                    var neko = JsonConvert.DeserializeObject<CuteGirlImage>(await response.Content.ReadAsStringAsync());
                    return neko.Url;
                }
            }
            
            return null;
        }

        public async Task<string> GetKitsuneImageAsync()
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetAsync(_creds.Website + "api/kitsune");
                if (response.IsSuccessStatusCode)
                {
                    var kitsune = JsonConvert.DeserializeObject<CuteGirlImage>(await response.Content.ReadAsStringAsync());
                    return kitsune.Url;
                }
            }

            return null;
        }
        
        private class CuteGirlImage
        {
            public string Url { get; set; }
        }
    }
}