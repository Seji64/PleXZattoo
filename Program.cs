using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using Serilog.Core;
using Serilog.Events;

namespace PleXZattoo
{
    class Program
    {
        static async Task Main(string[] args)
        {

            string out_file = String.Empty;
            string zattoo_username = String.Empty;
            string zattoo_password = String.Empty;
            bool debug = false;

            foreach (var arg in Environment.GetCommandLineArgs())
            {

                if (arg.ToLower().StartsWith("/outfile:"))
                {
                    out_file = arg.Replace("/outfile:", "");
                }

                if (arg.ToLower().StartsWith("/zattoo-username:"))
                {
                    zattoo_username = arg.Replace("/zattoo-username:", "");
                }

                if (arg.ToLower().StartsWith("/zattoo-password:"))
                {
                    zattoo_password = arg.Replace("/zattoo-password:", "");
                }

                if (arg.ToLower().StartsWith("/debug"))
                {
                    debug = true;
                }

            }

            try
            {

                var levelSwitch = new LoggingLevelSwitch();

                Log.Logger = new LoggerConfiguration()
                     .MinimumLevel.ControlledBy(levelSwitch)
                     .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                     .CreateLogger();

                if (debug){
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                }
                else{
                    levelSwitch.MinimumLevel = LogEventLevel.Information;
                }


                if (string.IsNullOrEmpty(zattoo_username) | string.IsNullOrEmpty(zattoo_password))
                {
                    throw new Exception("Zattoo Credentials must be provied!");
                }

                if (!zattoo_username.Contains("@"))
                {
                    throw new Exception("Zattoo Username is not a valid E-Mail address");
                }

                Log.Information("Loggin in...");

                string appToken = string.Empty;
                string sessionCookie = string.Empty;

                var baseAddress = new Uri("https://zattoo.com");
                var cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
                {

                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.67 Safari/537.36");

                    Log.Debug("Fetching AppToken...");

                    var result = await client.PostAsync("/", new StringContent(""));
                    result.EnsureSuccessStatusCode();

                    var match = Regex.Match(await result.Content.ReadAsStringAsync(), @"window\.appToken\s*=\s*'(.*)';");

                    if (match is null || match.Groups.Count < 2)
                    {
                        throw new Exception("failed to fetch AppToken");
                    }
                    else
                    {

                        appToken = match.Groups[1].Value;
                    }

                    Log.Debug("Done => {0}", appToken);

                    Log.Debug("Getting Session...");

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("lang", "en"),
                        new KeyValuePair<string, string>("client_app_token", appToken),
                        new KeyValuePair<string, string>("uuid", "d7512e98-38a0-4f01-b820-5a5cf98141fe"),
                        new KeyValuePair<string, string>("format", "json")
                    });

                    result = await client.PostAsync("https://zattoo.com/zapi/session/hello", content);

                    result.EnsureSuccessStatusCode();

                    foreach (Cookie cookie in cookieContainer.GetCookies(baseAddress))
                    {

                        if (cookie.Name.Equals("beaker.session.id"))
                        {
                            sessionCookie = cookie.Value;
                        }
                    }

                    if (string.IsNullOrEmpty(sessionCookie))
                    {
                        throw new Exception("Failed to get Session");
                    }

                    Log.Debug("Session Cookie => {0}", sessionCookie);
                    Log.Debug("Succesfully got Session");

                    content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("login", zattoo_username),
                        new KeyValuePair<string, string>("password", zattoo_password)
                    });

                    result = await client.PostAsync("https://zattoo.com/zapi/v2/account/login", content);

                    result.EnsureSuccessStatusCode();

                    var data = (JObject)JsonConvert.DeserializeObject(await result.Content.ReadAsStringAsync());

                    //Log.Debug(data.ToString());

                    if (!data["success"].Value<bool>())
                    {
                        throw new Exception("failed to login");

                    }

                    Log.Information("Login successfull");

                    string powerGuideHash = string.Empty;

                    if (data["session"]["power_guide_hash"] != null && !string.IsNullOrEmpty(data["session"]["power_guide_hash"].Value<String>()))
                    {
                        powerGuideHash = data["session"]["power_guide_hash"].Value<String>();
                    }
                    else
                    {
                        throw new Exception("Failed to get powerGuideHash");
                    }

                    Log.Debug("PowerGuideHash => {0}", powerGuideHash);

                    Log.Information("Getting Channels...");

                    //https://$provider/zapi/v2/cached/channels/$powerid?details=False 

                    result = await client.GetAsync(string.Format("https://zattoo.com/zapi/v2/cached/channels/{0}?details=False", powerGuideHash));

                    result.EnsureSuccessStatusCode();

                    //Log.Debug(await result.Content.ReadAsStringAsync());

                    Channels channelGroups = Channels.FromJson(await result.Content.ReadAsStringAsync());

                    if (!channelGroups.Success)
                    {
                        throw new Exception("Failed to get Channels");
                    }

                    Log.Information("Done - Found {0} Channel Groups", channelGroups.ChannelGroups.Count);

                    Log.Information("Generating M3u....");

                    StringBuilder playlist = new StringBuilder();

                    playlist.AppendLine("#EXTM3U");


                    foreach (var channelgrp in channelGroups.ChannelGroups)
                    {

                        Log.Debug("Processing Channel Group {0}", channelgrp.Name);

                        //Loop trough channels - skip radio and non availible channels
                        foreach (var channel in channelgrp.Channels.Where(mychannel => mychannel.IsRadio == false && mychannel.Qualities.First().Availability == Availability.Available))
                        {

                            int error_count = 0;
                            string entry = string.Empty;

                            Log.Debug("Processing {0} aka {1}", channel.Cid, channel.Title);

                            while (error_count < 3 & string.IsNullOrEmpty(entry))
                            {

                                try
                                {
                                    entry = await Util.ProcessChannel(client, channel, channelgrp.Name);
                                    //ZAPI Rate Limit - Wait a bit
                                    await Task.Delay(1000);
                                }
                                catch (Exception ex)
                                {
                                    error_count += 1;
                                    Log.Warning("[{0}/3]Failed to get HLS Stream Urlf for Channel {1} => {2}", error_count, channel.Cid, ex.Message);
                                }

                            }

                            if (string.IsNullOrWhiteSpace(entry))
                            {
                                Log.Error("Failed to process channel {0}", channel.Cid);
                            }
                            else
                            {
                                playlist.AppendLine(entry);
                            }

                        }

                    }


                    Log.Information("M3u8 generated! => Writing to disk...");

                    await System.IO.File.WriteAllTextAsync(out_file, playlist.ToString());

                    Log.Information("Done! - Bye");

                }

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }


        }
    }
}
