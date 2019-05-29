using System;

namespace vevodl {
	class Logger {
		private static void L(string msg, ConsoleColor fg_clr, ConsoleColor bg_clr = ConsoleColor.Black) {
			Console.ForegroundColor = fg_clr;
			Console.BackgroundColor = bg_clr;
			Console.WriteLine(msg);
			Console.ResetColor();
		}
		public static void Error(string msg) {
			L(msg, ConsoleColor.White, ConsoleColor.DarkRed);
		}
		public static void Info(string msg) {
			Console.ForegroundColor = ConsoleColor.Cyan;
			L(msg, ConsoleColor.Cyan);
		}
		public static void Debug(string msg) {
			if(System.Diagnostics.Debugger.IsAttached) {
				L(msg, ConsoleColor.DarkGray);
			}
		}
	}
}
