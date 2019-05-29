using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace vevodl {
	class ISRCDB {
		private static WebClient WC = new WebClient();
		private static string CSRF = string.Empty;
		private static string SESSIONID = string.Empty;

		/* API Settings */
		private const string API_Endpoint = "https://isrcsearch.ifpi.org/";
		private const int API_ResultsPerPage = 10;
		
		public static void GenerateSession() {
			if (string.IsNullOrEmpty(CSRF) || string.IsNullOrEmpty(SESSIONID)) {
				WC.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3";
				WC.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36";
				WC.Headers[HttpRequestHeader.Referer] = API_Endpoint;
				CSRF = Regex.Match(WC.DownloadString(API_Endpoint), "window.csrfmiddlewaretoken = \"([^\"]*)").Groups[1].Value;
				SESSIONID = Regex.Match(WC.ResponseHeaders[HttpResponseHeader.SetCookie], ",sessionid=([^;]*)").Groups[1].Value;
				WC.Dispose();
				WC = new WebClient();
			}
		}
		public static void Search(string Artist, string Title = "") {
			// Make sure we have CSRF and SESSIONID's
			GenerateSession();
			int Start = 0;
			Logger.Info(" - - - - - - - - - - - - - - - - - - - - - ");
			Logger.Info("Next Page: DPAD Right\nPrevious Page: DPAD Left\nTo start a download, copy the ISRC code and press DPAD Down to enter it.");
			while (true) {
				WC.Headers[HttpRequestHeader.Accept] = "application/json";
				WC.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36";
				WC.Headers[HttpRequestHeader.ContentType] = "application/json;charset=UTF-8";
				WC.Headers[HttpRequestHeader.Referer] = API_Endpoint;
				WC.Headers[HttpRequestHeader.Cookie] = "csrftoken=" + CSRF + "; sessionid=" + SESSIONID;
				WC.Headers["X-CSRFToken"] = CSRF;
				JToken Results = JToken.Parse(WC.UploadString(API_Endpoint + "/api/v1/search", "{\"searchFields\":{\"artistName\":\"" + Artist + "\",\"trackTitle\":\"" + Title + "\"},\"showReleases\":false,\"start\":" + Start + ",\"number\":" + API_ResultsPerPage + "}"));
				int Total = int.Parse(Results["numberOfHits"].ToString());
				int Offset = Start + API_ResultsPerPage;
				Logger.Info("Showing Results " + (Start + 1) + "-" + (Offset > Total ? Total : Offset) + " out of " + Total);
				int CurrentPage = (Start / 10) + 1;
				int TotalPages = (int)Math.Ceiling((double)Total / 10);
				if (CurrentPage == TotalPages) {
					break;
				}
				Logger.Info("Page " + CurrentPage + "/" + TotalPages);
				foreach (JToken R in Results["displayDocs"]) {
					Logger.Info(R["isrcCode"] + " :: " + R["artistName"] + " - " + R["trackTitle"] + " [" + (R["recordingYear"].ToString() != string.Empty ? R["recordingYear"] : "----") + "]" + (R["recordingVersion"].ToString() != string.Empty ? " (" + R["recordingVersion"] + ")" : string.Empty));
				}
				Logger.Info(" - - - - - - - - - - - - - - - - - - - - - ");
				inputCheck:
				ConsoleKey Input = Console.ReadKey().Key;
				switch (Input) {
					case ConsoleKey.DownArrow:
					return;
					case ConsoleKey.RightArrow:
					Program.DeleteConsoleLines(Results["displayDocs"].Count() + 3);
					Start += API_ResultsPerPage;
					break;
					case ConsoleKey.LeftArrow:
					Program.DeleteConsoleLines(Results["displayDocs"].Count() + 3);
					Start -= API_ResultsPerPage;
					break;
					default:
					Program.DeleteCurrentLine();
					goto inputCheck;
				}

			}
		}
	}
}
