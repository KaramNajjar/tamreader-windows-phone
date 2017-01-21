using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;

namespace tamreader.Logic
{
    public class TamreaderClient
    {
        static HttpClient client;

        public TamreaderClient()
        {
            client = new HttpClient();
            client.BaseAddress = new Uri("http://tamreaderapi.azurewebsites.net/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        }
        public async Task<Uri> CreateSignAsync(Sign sign)
        {
            try
            {
                HttpResponseMessage response = await client.PostAsJsonAsync("signs", sign);
                response.EnsureSuccessStatusCode();
                return response.Headers.Location;
            }catch(Exception)
            {
                return new Uri("");
            }

        }

        public static async Task<IEnumerable<Sign>> GetSignsAsync(string path)
        {
            HttpClient client = new HttpClient();
            IEnumerable<Sign> s = null;
            HttpResponseMessage response = await client.GetAsync(path);
            if (response.IsSuccessStatusCode)
            {
                s = await response.Content.ReadAsAsync<IEnumerable<Sign>>();
            }
            return s;
        }

    }
}
