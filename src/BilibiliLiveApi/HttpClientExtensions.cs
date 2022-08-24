using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;


namespace System.Net.Http
{
    public static class HttpClientExtensions
    {
        static ConcurrentDictionary<string, CancellationTokenSource> Dic412 = new();
        public static async Task<JToken> ReadAsJTokenAsync(this HttpResponseMessage httpResponseMessage)
        {
            return JToken.Parse(await httpResponseMessage.EnsureSuccessStatusCode().Content.ReadAsStringAsync().ConfigureAwait(false));
        }
        public static async Task<JToken> GetJTokenAsync(this HttpClient client, string requestUri)
        {
            if (requestUri.Split('=', 2)[0] is var url && (!Dic412.TryGetValue(url, out var c) || (c.IsCancellationRequested && (Dic412.Remove(url, out _) || true))))
            {
                try
                {
                    var resp = await client.GetAsync(requestUri);
                    return await resp.ReadAsJTokenAsync();
                }
                catch (HttpRequestException e) when (e.Message.Contains("412"))
                {
                    Dic412.TryAdd(url, new CancellationTokenSource(TimeSpan.FromMinutes(5)));
                    throw;
                }
            }
            else
            {
                throw new HttpRequestException("Response status code does not indicate success: 412 (Precondition Failed).");
            }
        }
    }
}
