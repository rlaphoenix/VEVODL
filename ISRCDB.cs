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
			// Print Header, this just explains how the searcher works
			SlatePrint(new string[] {
				"- - - - - - - - - - - - - - - - - - - - -",
				"         = ISRCDB via IFPI.org =         ",
				"- - - - - - - - - - - - - - - - - - - - -",
				"Next Page: DPAD Right",
				"Previous Page: DPAD Left",
				"Download ISRC: DPAD Down",
				"To download every ISRC from this page onward: DPAD Up (this can take a while!)."
			});
			bool AddAll = false;
			int Start = 0;
			// Create a list to hold results
			List<(string, string)> Results = new List<(string, string)>();
			// Loop to continue with stuff like Dpad Left/Right/Up
			bool Searching = true;
			while (Searching) {
				// Set headers for the request, for some reason they need to be set every single request
				WC.Headers[HttpRequestHeader.Accept] = "application/json";
				WC.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.36";
				WC.Headers[HttpRequestHeader.ContentType] = "application/json;charset=UTF-8";
				WC.Headers[HttpRequestHeader.Referer] = API_Endpoint;
				WC.Headers[HttpRequestHeader.Cookie] = "csrftoken=" + CSRF + "; sessionid=" + SESSIONID;
				WC.Headers["X-CSRFToken"] = CSRF;
				JToken Response = JToken.Parse(WC.UploadString(API_Endpoint + "/api/v1/search", "{\"searchFields\":{\"artistName\":\"" + Artist + "\",\"trackTitle\":\"" + Title + "\"},\"showReleases\":false,\"start\":" + Start + ",\"number\":" + API_ResultsPerPage + "}"));
				int Total = Response.Value<int>("numberOfHits");
				int Showing = Response["displayDocs"].Count();
				// Don't print stuff if we are using DPAD Up batch DL mode
				if(!AddAll) {
					// Show some statistics
					SlatePrint(new string[] {
					"Showing Results " + (Start + 1) + "-" + (Start + Showing) + " out of " + Total,
					"Page " + ((Start / API_ResultsPerPage) + 1) + "/" + ((int)Math.Ceiling((double)Total / API_ResultsPerPage)),
					string.Empty //Space it out to the results a tad bit
				});
					// Output each result
					for (int i = 0; i < Showing; i++) {
						JToken R = Response["displayDocs"][i];
						string ISRC = R["isrcCode"].ToString();
						string ISRC_RegistrantCode = new string(ISRC.Skip(2).Take(3).ToArray());
						string RArtist = R["artistName"].ToString();
						string RTitle = R["trackTitle"].ToString();
						string Version = R["recordingVersion"].ToString();
						Console.ForegroundColor = (ISRC_RegistrantCode == "UV7" || ISRC_RegistrantCode == "IV2") && RArtist.ToLowerInvariant().Contains(Artist.ToLowerInvariant()) && RTitle.ToLowerInvariant().Contains(Title.ToLowerInvariant()) ? ConsoleColor.White : ConsoleColor.DarkGray;
						Console.WriteLine(" " + (i + 1).ToString("000") + " :: " + ISRC + " :: " + RArtist + " - " + RTitle + " [" + (R["recordingYear"].ToString() != string.Empty ? R["recordingYear"] : "----") + "]" + (Version != string.Empty ? " (" + Version + ")" : string.Empty));
						Console.ResetColor();
					}
					// Show a line to indicate end of the results clearer
					Logger.Info(" - - - - - - - - - - - - - - - - - - - - - ");
				}
				Controls:
				switch (AddAll ? ConsoleKey.UpArrow : Console.ReadKey().Key) {
					case ConsoleKey.UpArrow:
					Results.AddRange(Response["displayDocs"].Select(x => new ValueTuple<string, string>(x["isrcCode"].ToString(), x["recordingVersion"].ToString())).ToArray());
					Console.Clear();
					Console.WriteLine(" Scraping " + API_Endpoint + " for a list of all ISRC Codes for the search query you made: \"" + string.Join(" - ", new string[] { Artist, Title }.Where(x => !string.IsNullOrEmpty(x))) + "\"\n... this may take a while owo");
					// If its the end of the line, let's exit the while loop to return the finished list
					if (Start + Showing == Total) {
						Searching = false;
						break;
					}
					Start += API_ResultsPerPage;
					AddAll = true;
					break;
					case ConsoleKey.DownArrow:
					JToken SelectedRelease = Response["displayDocs"][int.Parse(Program.Ask<string>("Which ISRC do you want to download? (#)", Program.AskSettings.None, "Number Choice", @"^[1-9][0-9]?$|^100$")) - 1];
					Results.Add((SelectedRelease["isrcCode"].ToString(), SelectedRelease["recordingVersion"].ToString()));
					Searching = false;
					break;
					case ConsoleKey.LeftArrow:
					if (Start != 0) {
						Program.DeleteConsoleLines(Showing + 5);
						Start -= API_ResultsPerPage;
					}
					break;
					case ConsoleKey.RightArrow:
					if (Showing != Total) {
						Program.DeleteConsoleLines(Showing + 5);
						Start += API_ResultsPerPage;
					}
					break;
					default:
					Program.DeleteCurrentLine();
					goto Controls;
					break;
				}
			}
			return Results;
		}
		private static void SlatePrint(string[] Lines) {
			Logger.Info(" " + string.Join("\n ", Lines));
		}
	}
}
