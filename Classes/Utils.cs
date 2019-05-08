using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace PleXZattoo
{

    public class Util
    {


        public static async Task<string> ProcessChannel(HttpClient client, Channel channel, string channelGroupName)
        {
            StringBuilder playlistentry = new StringBuilder();

            string logo_black = channel.Qualities.First().LogoBlack84;

            Log.Debug("Getting Stream URL for {0}", channel.Cid);


            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("cid", channel.Cid),
                new KeyValuePair<string, string>("stream_type", "hls"),
                new KeyValuePair<string, string>("https_watch_urls", "True"),
                new KeyValuePair<string, string>("timeshift", "10800")
            });

            var result = await client.PostAsync("https://zattoo.com/zapi/watch", content);

            result.EnsureSuccessStatusCode();

            Streams channelStreams = Streams.FromJson(await result.Content.ReadAsStringAsync());

            if (!channelStreams.Success)
            {
                throw new Exception("Failed to get channelStreams");
            }

            string streamURL = await Util.GetHLSStreamURL(channelStreams);

            if (string.IsNullOrWhiteSpace(streamURL))
            {
                throw new Exception("streamURL cannot be empty");
            }

            Log.Debug("Successfully retrived streamURL => {0}", streamURL);


            playlistentry.AppendLine(string.Format("#EXTINF:-1 tvg-id=\"{0}\" tvg-name=\"{1}\" tvg-logo=\"http://images.zattic.com{2}\" group-title=\"{3}\", {4}", channel.Cid, channel.Title, logo_black, channelGroupName, channel.Title));
            playlistentry.Append(streamURL);

            return playlistentry.ToString();

        }
        public static async Task<string> GetHLSStreamURL(Streams channelStreams)
        {

            string channelStreamURL = string.Empty;

            try
            {
                var baseAddress = new Uri("https://zattoo.com");
                var cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                {

                    var result = await client.GetAsync(channelStreams.Stream.Url);

                    result.EnsureSuccessStatusCode();

                    string body = await result.Content.ReadAsStringAsync();

                    //BANDWIDTH=5000000\n(.*[0-9]{3,4}-.*).m3u8\?(z32=[A-Z0-9]+)

                    MatchCollection matches = Regex.Matches(await result.Content.ReadAsStringAsync(), @"\n(.*[0-9]{3,4}-.*).m3u8\?(z32=[A-Z0-9]+)");

                    if (matches.Count == 0 || matches[0].Success == false || string.IsNullOrWhiteSpace(matches[0].Value))
                    {
                        throw new Exception("Invalid response");
                    }

                    string streamUrlPathAndQuery = matches[0].Value.TrimStart();

                    channelStreamURL = string.Format("http://{0}/{1}", channelStreams.Stream.Url.Host, streamUrlPathAndQuery);

                }

                return channelStreamURL;

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

            }

            return channelStreamURL;
        }

    }

}
