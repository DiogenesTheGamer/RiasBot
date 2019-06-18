using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RiasBot.Commons.Attributes;
using RiasBot.Services;

namespace RiasBot.Modules.Reactions.Services
{
    [Service]
    public class ReactionsService
    {
        private readonly IBotCredentials _creds;

        public ReactionsService(IBotCredentials creds)
        {
            _creds = creds;
        }

        public async Task<string> GetReactionAsync(string type, string fileType)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add("Authorization", "Wolke " + _creds.WeebServicesToken);
                http.DefaultRequestHeaders.Add("User-Agent", "RiasBot/" + RiasBot.Version);
                var request = await http.GetAsync($"{_creds.WeebApi}images/random?type={type}&filetype={fileType}");
                if (request.IsSuccessStatusCode)
                {
                    var patImage = JsonConvert.DeserializeObject<WeebServices>(await request.Content.ReadAsStringAsync());
                    return patImage.Url;
                }
            }

            return null;
        }
        
        public async Task<string> GetGropeImage()
        {
            using (var http = new HttpClient())
            {
                var gropeRequest = await http.GetAsync(_creds.Website + "api/grope");
                if (gropeRequest.IsSuccessStatusCode)
                {
                    var gropeImage = JsonConvert.DeserializeObject<Dictionary<string, string>>(await gropeRequest.Content.ReadAsStringAsync());
                    return gropeImage["url"];
                }  
            }

            return null;
        }
        
        private class WeebServices
        {
            //public int Status { get; }
            //public string Id { get; }
            //public string Type { get; }
            //public string BaseType { get; }
            //public bool Nsfw { get; }
            //public string FileType { get; }
            //public string MimeType { get; }
            //public string Account { get; }
            //public bool Hidden { get; }
            //public string[] Tags { get; }
            public string Url { get; set; }
        }
    }
}