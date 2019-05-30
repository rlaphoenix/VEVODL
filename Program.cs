using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace vevodl {
	class Program {

		static string ROOTDIR = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

		static void Main(string[] args) {

			while(true) {

				List<(string ISRC, string Version)> Releases = null;

				Console.Write("[Search for an ISRC? (y/n)]: ");
				ConsoleKey Input = Console.ReadKey().Key;
				DeleteCurrentLine();
				if (Input == ConsoleKey.Y) {
					Console.Write("[Artist]: ");
					string Artist = Console.ReadLine();
					Console.Write("[Title]: ");
					string Title = Console.ReadLine();
					Releases = ISRCDB.Search(Artist, Title);
				} else {
					bool InvalidISRC = false;
					do {
						if (InvalidISRC) {
							Logger.Error(Releases.First().ISRC + " is an invalid ISRC string...");
						}
						Console.Write("[ISRC]: ");
						Releases = new List<(string, string)> { (Console.ReadLine().ToUpperInvariant(), string.Empty) };
						DeleteConsoleLines(1);
					} while (Releases == null || (InvalidISRC = !Regex.Match(Releases.First().ISRC, "^[A-Z]{2}-?\\w{3}-?\\d{2}-?\\d{5}$").Success));
				}

				foreach ((string ISRC, string Version) Release in Releases) {
					string ISRC = Release.ISRC;
					string Version = VEVO.Sanitize(Release.Version);
					#region Attempt to Query the ISRC to VEVO
					if (!VEVO.Query(ISRC)) {
						continue;
					}
					#endregion
					#region Download HLS Catalogue to MKV File
					string Filename = VEVO.Artist + " - " + VEVO.Title + (Version != string.Empty ? " [" + Version + "]" : string.Empty) + " [" + ISRC + "]";
					string HLSCatalogue = VEVO.HLSCatalogue;
					Logger.Info(" :=: Downloading " + ISRC + " as \"" + Filename + ".mkv\" :=:");
					#region Subtitles
					if (!string.IsNullOrEmpty(VEVO.Subtitle)) {
						#region Download Subtitle M3U8 File as a VTT
						DownloadM3U8(VEVO.Subtitle, "vtt");
						#endregion
						#region Fix VTT Subtitle
						// For some reason the VTT has a ton of garbage not parsed properly with ffmpeg, no idea who to fault but they manually need to be removed.
						// "WEBVTT FILE" line has an odd character in front of it, so a .EndsWith is needed.
						// The timestamp line may be specific to each video im not sure.
						File.WriteAllLines("temp/.vtt", File.ReadAllLines("temp/.vtt", Encoding.UTF8).Select(l => l.Trim()).Where(l => l != "WEBVTT" && !l.EndsWith("WEBVTT FILE") && l != "X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000"), Encoding.UTF8);
						#endregion
						#region Convert VTT to SRT
						if (RunEXE("SubtitleEdit.exe", "/convert \"temp/.vtt\" srt /overwrite") != 0) {
							Logger.Error("Failed to convert the VTT subtitles to SRT, ignoring subtitles and continuing without them!");
							if (File.Exists("temp/.vtt")) {
								File.Delete("temp/.vtt");
							}
							if (File.Exists("temp/.srt")) {
								File.Delete("temp/.srt");
							}
						}
						#endregion
					}
					#endregion
					#region TS (Video/Audio)
					DownloadM3U8(VEVO.TS);
					#endregion
					#region Mux everything into an MKV
					if (RunEXE("mkvmerge.exe", "--output \"" + Path.Combine(ROOTDIR, Filename.Replace("/", "-") + ".mkv") + "\" \"" + Path.Combine(ROOTDIR, "temp", ".ts") + "\" --default-track 0:false " + (File.Exists("temp/.chapters") ? "--chapters \"" + Path.Combine(ROOTDIR, "temp", ".chapters") + "\"" : string.Empty) + " " + (File.Exists("temp/.srt") ? "--sub-charset 0:UTF-8 \"" + Path.Combine(ROOTDIR, "temp", ".srt") + "\"" : string.Empty)) != 0) {
						return;
					}
					// Cleanup files no longer needed
					// todo: setup files in such a way to be multi-threaded supported and not conflict with other downloads at same time
					File.Delete("temp/.ts");
					File.Delete("temp/.srt");
					File.Delete("temp/.chapters");
					#endregion
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("Downloaded!");
					Console.ResetColor();
					#endregion
				}

			}
		}
		public static void DeleteConsoleLines(int Lines) {
			for (int i = 0; i < Lines; i++) {
				Console.SetCursorPosition(0, Console.CursorTop - 1);
				Console.Write(new string(' ', Console.WindowWidth));
				Console.SetCursorPosition(0, Console.CursorTop - 1);
			}
		}
		public static void DeleteCurrentLine() {
			Console.SetCursorPosition(0, Console.CursorTop); // Set cursor to start of current line
			Console.Write(new string(' ', Console.WindowWidth)); // Fill the entire line with spaces
			Console.SetCursorPosition(0, Console.CursorTop - 1); // Set cursor to start of previous line
		}
		private static int RunEXE(string exePath, string args) {
			//todo: find a more convenient better looking way to ignore all output and windows then using redirect like this
			Process p = new Process {
				StartInfo = new ProcessStartInfo(Path.Combine(ROOTDIR, "tools", exePath)) {
					Arguments = args,
					UseShellExecute = false,
					CreateNoWindow = false,
					RedirectStandardError = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true
				}
			};
			p.Start();
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();
			p.WaitForExit();
			if (p.ExitCode != 0) {
				Logger.Error(Path.GetFileName(exePath) + " closed with an error code :( (Something unexpected went wrong)");
			}
			return p.ExitCode;
		}
		private static void DownloadM3U8(string URL, string Type = "ts") {
			foreach (string dir in new[] { "temp", "temp/seg" }) {
				Directory.CreateDirectory(dir);
			}
			string M3U8 = null;
			using (WebClient WC = new WebClient()) {
				// Get M3U8 File's Contents via GET
				M3U8 = WC.DownloadString(URL);
			}
			// Download Segments and replace the M3U8 File Content's segment paths to the local downloaded relative paths
			ConcurrentDictionary<string, string> segMap = new ConcurrentDictionary<string, string>();
			Parallel.ForEach(
				Regex.Matches(M3U8, "#EXTINF:.*\\s(.*)").Cast<Match>().Select(x => x.Groups[1].Value).Distinct(),
				seg => {
					string fn = "temp/seg/" + seg.GetHashCode().ToString().Replace("-", "m") + ".ts";
					bool downloaded = false;
					while (!downloaded) {
						try {
							// Cannot reference an already instanciated WebClient here as WebClient doesnt support I/O (Cant do multi-threaded operations)
							new WebClient().DownloadFile((!seg.StartsWith("http") ? URL.Substring(0, URL.LastIndexOf('/') + 1) : string.Empty) + seg, fn);
							segMap.TryAdd(seg, fn.Replace("temp/", string.Empty));
							downloaded = true;
						} catch (Exception ex) {
							Logger.Error("Failed while downloading \"" + seg + "\", Retrying, Error Message: " + ex.Message);
						}
					}
				}
			);
			foreach (KeyValuePair<string, string> seg in segMap) {
				M3U8 = M3U8.Replace(seg.Key, seg.Value);
			}
			// Write new M3U8 content to a file so FFMPEG can read it
			File.WriteAllText("temp/" + Type + ".m3u8", M3U8);
			// Run FFMPEG on the new M3U8 to let it compile them all as a single Matroska format file dealing with crap like timing, order, timecodes, audio, muxing, and shit.
			if (RunEXE("ffmpeg.exe", "-protocol_whitelist file,http,https,tcp,tls,crypto -allowed_extensions ALL -y -hide_banner -i \"temp/" + Type + ".m3u8\" -c copy \"temp/." + Type + "\"") != 0) {
				return;
			}
			// Delete now un-needed data
			foreach (string dir in new[] { "temp/seg" }) {
				Directory.Delete(dir, true);
			}
			File.Delete("temp/" + Type + ".m3u8");
		}
	}
}
