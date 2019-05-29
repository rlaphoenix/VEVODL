using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace vevodl {
	class VEVO {
		#region Public Properties
		public static JToken Metadata => _Query["basicMeta"];
		public static JToken Streams => _Query["streams"];
		public static string Artist => Metadata["artists"].First(x => x["basicMeta"]["role"].ToString() == "Main")["basicMeta"]["name"].ToString();
		public static string Title => Metadata["title"].ToString();
		public static string HLSCatalogue => new WebClient().DownloadString(Streams.Where(x => x["url"].ToString().ToLowerInvariant().EndsWith(".m3u8")).First()["url"].ToString());
		public static string Subtitle => Regex.Match(HLSCatalogue, "#EXT-X-MEDIA:TYPE=SUBTITLES.*?,URI=\"([^\"]*)").Groups[1].Value;
		public static string TS => Regex.Matches(HLSCatalogue, "#EXT-X-STREAM.*?BANDWIDTH=([^,]*),RESOLUTION=[^x]*x([^,]*).*\\s(.*)").Cast<Match>().OrderByDescending(x => int.Parse(x.Groups[1].Value)).OrderByDescending(x => int.Parse(x.Groups[2].Value)).First().Groups[3].Value;
		#endregion
		#region Token
		private static string _Token = null;
		private static string Token() {
			if(_Token != null) {
				return _Token;
			}
			using (WebClient WC = new WebClient()) {
				WC.Headers[HttpRequestHeader.Accept] = "*/*";
				WC.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36";
				WC.Headers[HttpRequestHeader.ContentType] = "application/json";
				WC.Headers[HttpRequestHeader.Referer] = "https://embed.vevo.com/?video=&autoplay=0";
				return _Token = JToken.Parse(WC.UploadString("https://accounts.vevo.com/token", "{\"client_id\":\"SPupX1tvqFEopQ1YS6SS\",\"grant_type\":\"urn:vevo:params:oauth:grant-type:anonymous\"}"))["access_token"].ToString();
			}
		}
		#endregion
		#region Public Functions
		private static JToken _Query = null;
		public static bool Query(string ISRC) {
			using(WebClient WC = new WebClient()) {
				WC.Headers[HttpRequestHeader.Accept] = "*/*";
				WC.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36";
				WC.Headers[HttpRequestHeader.ContentType] = "application/json";
				WC.Headers[HttpRequestHeader.Referer] = "https://embed.vevo.com/?video=" + ISRC + "&autoplay=0";
				WC.Headers["Vevo-Device"] = "Desktop";
				WC.Headers["Vevo-OS"] = "Windows/10";
				WC.Headers["Vevo-Product"] = "web-syndication/0";
				WC.Headers[HttpRequestHeader.Authorization] = "Bearer " + Token();
				JToken Result = JToken.Parse(WC.UploadString("https://veil.vevoprd.com/graphql", "{\"query\":\"query Video($ids: [String]!, $partnerId: String) {\\n  videos(ids: $ids, partnerId: $partnerId) {\\n    data {\\n      id\\n      basicMeta {\\n        artists {\\n          basicMeta {\\n            name\\n            role\\n            urlSafeName\\n            thumbnailUrl\\n            links {\\n              url\\n              userName\\n              type\\n              __typename\\n            }\\n            __typename\\n          }\\n          __typename\\n        }\\n        isMonetizable\\n        isrc\\n        title\\n        urlSafeTitle\\n        copyright\\n        shortUrl\\n        thumbnailUrl\\n        duration\\n        hasLyrics\\n        allowEmbed\\n        allowMobile\\n        isUnlisted\\n        isOfficial\\n        releaseDate\\n        categories\\n        credits {\\n          name\\n          value\\n          __typename\\n        }\\n        errorCode\\n        __typename\\n      }\\n      likes\\n      liked\\n      views {\\n        viewsTotal\\n        __typename\\n      }\\n      streams {\\n        quality\\n        url\\n        errorCode\\n        __typename\\n      }\\n      __typename\\n    }\\n    __typename\\n  }\\n}\\n\",\"variables\":{\"ids\":[\"" + ISRC.ToLowerInvariant() + "\"]},\"operationName\":\"Video\"}"))["data"]["videos"]["data"][0];
				if (Result["streams"][0]["errorCode"].ToString() == "404") {
					Console.WriteLine(ISRC + " isn't available on VEVO's Systems...");
					return false;
				}
				//Logger.Error("Failed to Query ISRC of " + ISRC + " on VEVO's Servers. Retrying in 5 seconds...");
				//Thread.Sleep(5000);
				_Query = Result;
			}
			return true;
		}
		#endregion
	}
}