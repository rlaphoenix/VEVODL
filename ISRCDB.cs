using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
		private const int API_ResultsPerPage = 100;
		
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
		public static List<(string, string)> Search(string Artist, string Title = "") {
			// Make sure we have CSRF and SESSIONID's
			GenerateSession();
			List<(string, string)> ISRCsToDownload = new List<(string, string)>();
			int Start = 0;
			ConsoleKey Input = ConsoleKey.NoName;
			Logger.Info(" - - - - - - - - - - - - - - - - - - - - - ");
			Logger.Info("Next Page: DPAD Right\nPrevious Page: DPAD Left\nTo download an ISRC from this page, press DPAD Down.\nTo try and download every ISRC returned by the query, press DPAD Up (this can take a while!).");
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
				int l = Start;
				foreach (JToken R in Results["displayDocs"]) {
					string ISRC = R["isrcCode"].ToString();
					string ISRC_RegistrantCode = new string(ISRC.Skip(2).Take(3).ToArray());
					string ISRC_DesignationCode = new string(ISRC.Skip(7).Take(5).ToArray());
					string RArtist = R["artistName"].ToString();
					string RTitle = R["trackTitle"].ToString();
					string Version = R["recordingVersion"].ToString();
					Console.ForegroundColor = (ISRC_RegistrantCode == "UV7" || ISRC_RegistrantCode == "IV2") && RArtist.ToLowerInvariant().Contains(Artist.ToLowerInvariant()) && RTitle.ToLowerInvariant().Contains(Title.ToLowerInvariant()) ? ConsoleColor.White : ConsoleColor.DarkGray;
					Console.WriteLine((++l).ToString("000") + " :: " + ISRC + " :: " + RArtist + " - " + RTitle + " [" + (R["recordingYear"].ToString() != string.Empty ? R["recordingYear"] : "----") + "]" + (Version != string.Empty ? " (" + Version + ")" : string.Empty));
					Console.ResetColor();
				}
				Logger.Info(" - - - - - - - - - - - - - - - - - - - - - ");
				inputCheck:
				if(Input != ConsoleKey.UpArrow) {
					Input = Console.ReadKey().Key;
				}
				switch (Input) {
					case ConsoleKey.DownArrow:
					Logger.Info("[Which ISRC do you want to download? (#)]: ");
					JToken SelectedRelease = Results["displayDocs"][int.Parse(Console.ReadLine()) - 1];
					return new List<(string, string)> { (SelectedRelease["isrcCode"].ToString(), SelectedRelease["recordingVersion"].ToString()) };
					case ConsoleKey.UpArrow:
					if(Start == (Total - API_ResultsPerPage)) {
						return ISRCsToDownload;
					}
					ISRCsToDownload.AddRange(Results["displayDocs"].Select(x => new ValueTuple<string, string>(x["isrcCode"].ToString(), x["recordingVersion"].ToString())).ToArray());
					Program.DeleteConsoleLines(Results["displayDocs"].Count() + 3);
					Start += API_ResultsPerPage;
					break;
					case ConsoleKey.RightArrow:
					if(Start != (Total - API_ResultsPerPage)) {
						Program.DeleteConsoleLines(Results["displayDocs"].Count() + 3);
						Start += API_ResultsPerPage;
					}
					break;
					case ConsoleKey.LeftArrow:
					if(Start != 0) {
						Program.DeleteConsoleLines(Results["displayDocs"].Count() + 3);
						Start -= API_ResultsPerPage;
					}
					break;
					default:
					Program.DeleteCurrentLine();
					goto inputCheck;
				}
			}
			return null;
		}
	}
}
