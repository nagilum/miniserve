using dotless.Core;
using dotless.Core.configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace miniserve {
	public class Program {
		/// <summary>
		/// The keeper of config.
		/// </summary>
		public static Config config;

		/// <summary>
		/// Base path for the config file.
		/// </summary>
		public static string configPath;

		/// <summary>
		/// Compiled LESS.
		/// </summary>
		public static string compiledCSS;

		/// <summary>
		/// Compiled JS.
		/// </summary>
		public static string compiledJS;

		/// <summary>
		/// Main program entry point.
		/// </summary>
		public static void Main(string[] args) {
			var configFile = string.Join(" ", args);

			if (string.IsNullOrWhiteSpace(configFile)) {
				Console.WriteLine("No config file supplied");
				return;
			}

			if (!File.Exists(configFile)) {
				Console.WriteLine("Could not file {0}", configFile);
				return;
			}

			var configJSON = File.ReadAllText(configFile);

			try {
				// Attempt to parse the config JSON info a working Config object.
				config = new JavaScriptSerializer().Deserialize<Config>(configJSON);
			}
			catch {
				Console.WriteLine("Error parsing config file {0}", configFile);
				return;
			}

			if (config == null) {
				Console.WriteLine("Parse of config file was unsuccessful");
				return;
			}

			configPath = Path.GetDirectoryName(configFile);

			if (string.IsNullOrWhiteSpace(configPath)) {
				Console.WriteLine("Unable to parse path from supplied config file");
				return;
			}

			var appPath = config.Path;
			var appPathIsAbs = appPath.Substring(1, 1) == ":" || appPath.StartsWith("\\");

			if (!appPathIsAbs)
				appPath = Path.Combine(
					configPath,
					appPath);

			if (config.Port == 0)
				config.Port = 80;

			if (config.Automations != null) {
				// Set the default wait time to 100 MS.
				foreach (var automation in config.Automations.Where(automation => automation.WaitBeforeParsing == 0))
					automation.WaitBeforeParsing = 100;

				// Cycle each automation and setup FSW.
				foreach (var automation in config.Automations) {
					if (string.IsNullOrWhiteSpace(automation.Type)) {
						Console.WriteLine("You must specify a valid type for automation; html/less/js");
						return;
					}

					if (automation.SourceFiles == null || !automation.SourceFiles.Any()) {
						Console.WriteLine("No source files specified for type: " + automation.Type);
						return;
					}

					foreach (var input in automation.SourceFiles) {
						var path = string.Empty;
						var pattern = "*." + automation.Type.ToLower();

						if (input.IndexOf('*') > -1) {
							if (input.IndexOf('/') > -1) {
								path = input.Substring(0, input.LastIndexOf('/'));
								pattern = input.Substring(input.LastIndexOf('/') + 1);
							}
							else {
								pattern = input;
							}
						}
						else {
							path = input;
						}

						path = path.Replace("/", "\\");

						var pathIsAbs = path.Substring(1, 1) == ":" || path.StartsWith("\\");

						if (!pathIsAbs)
							path = Path.Combine(
								configPath,
								path);

						if (!Directory.Exists(path)) {
							pattern = path.Substring(path.LastIndexOf("\\", StringComparison.InvariantCultureIgnoreCase) + 1);
							path = path.Substring(0, path.LastIndexOf("\\", StringComparison.InvariantCultureIgnoreCase));
						}

						if (automation.fswEntries == null)
							automation.fswEntries = new List<ConfigAutomationFSW>();

						automation.fswEntries.Add(
							new ConfigAutomationFSW {
								Path = path,
								Pattern = pattern
							});

						var fsw = new FileSystemWatcher {
							Path = path,
							IncludeSubdirectories = true,
							Filter = pattern
						};

						fsw.Changed += (s, a) => { CompileSource(); };
						fsw.Created += (s, a) => { CompileSource(); };
						fsw.Deleted += (s, a) => { CompileSource(); };
						fsw.Renamed += (s, a) => { CompileSource(); };
						fsw.EnableRaisingEvents = true;
					}
				}

				// Compile JS/LESS/HTML on startup.
				CompileSource();
			}

			var httpServer = new SimpleHTTPServer(
				appPath,
				config.Port);

			Console.WriteLine("Server is running on this port: {0}", httpServer.Port);
			Console.WriteLine("Press any key to exit.");
			Console.ReadKey();

			httpServer.Stop();
		}

		/// <summary>
		/// Compile JS/LESS/HTML.
		/// </summary>
		private static void CompileSource() {
			CompileJS();
			CompileLESS();
			CompileHTML();
		}

		/// <summary>
		/// Compile HTML.
		/// </summary>
		private static void CompileHTML() {
			if (config.Automations == null)
				return;

			foreach (var automation in config.Automations.Where(a => a.Type.ToLower() == "html")) {
				if (automation.WaitBeforeParsing > 0)
					Thread.Sleep(automation.WaitBeforeParsing);

				if (automation.fswEntries == null)
					continue;

				var html = string.Empty;

				foreach (var entry in automation.fswEntries) {
					var files = GetFiles(
						entry.Path,
						entry.Pattern);

					html += GetFileContents(files);
				}

				if (automation.ParseTags)
					html = html
						.Replace("{{ css }}", string.Format("<style type=\"text/css\">{0}</style>", compiledCSS))
						.Replace("{{ js }}", string.Format("<script>{0}</script>", compiledJS));

				if (automation.Minify) {
					var lines = html.Split('\r');

					if (lines.Length == 1)
						lines = html.Split('\n');

					html = lines.Aggregate(string.Empty, (current, line) => current + line.Trim());
				}

				var path = automation.DestFile;

				if (string.IsNullOrWhiteSpace(path)) {
					Console.WriteLine("No destFile specified for HTML type");
					continue;
				}

				var pathIsAbs = path.Substring(1, 1) == ":" || path.StartsWith("\\");

				if (!pathIsAbs)
					path = Path.Combine(
						configPath,
						path);

				try {
					File.WriteAllText(
						path,
						html);
				}
				catch {
					Console.WriteLine("Could not write JS to dest file {0}", automation.DestFile);
				}
			}
		}

		/// <summary>
		/// Compile JS.
		/// </summary>
		private static void CompileJS() {
			if (config.Automations == null)
				return;

			compiledJS = string.Empty;

			foreach (var automation in config.Automations.Where(a => a.Type.ToLower() == "js")) {
				if (automation.WaitBeforeParsing > 0)
					Thread.Sleep(automation.WaitBeforeParsing);

				if (automation.fswEntries == null)
					continue;

				var js = string.Empty;

				foreach (var entry in automation.fswEntries) {
					var files = GetFiles(
						entry.Path,
						entry.Pattern);

					js += GetFileContents(files);
				}

				try {
					if (automation.Minify)
						js = new JavaScriptMinifier().Minify(js);
				}
				catch {
					Console.WriteLine("Could not minify JS");
				}

				compiledJS += js;

				var path = automation.DestFile;

				if (string.IsNullOrWhiteSpace(path))
					continue;

				var pathIsAbs = path.Substring(1, 1) == ":" || path.StartsWith("\\");

				if (!pathIsAbs)
					path = Path.Combine(
						configPath,
						path);

				try {
					File.WriteAllText(
						path,
						js);
				}
				catch {
					Console.WriteLine("Could not write JS to dest file {0}", automation.DestFile);
				}
			}
		}

		/// <summary>
		/// Compile LESS.
		/// </summary>
		private static void CompileLESS() {
			if (config.Automations == null)
				return;

			compiledCSS = string.Empty;

			foreach (var automation in config.Automations.Where(a => a.Type.ToLower() == "less")) {
				if (automation.WaitBeforeParsing > 0)
					Thread.Sleep(automation.WaitBeforeParsing);

				if (automation.fswEntries == null)
					continue;

				var less = string.Empty;
				var css = string.Empty;

				foreach (var entry in automation.fswEntries) {
					var files = GetFiles(
						entry.Path,
						entry.Pattern);

					less += GetFileContents(files);
				}

				var lessConfig = new DotlessConfiguration {
					MinifyOutput = automation.Minify
				};

				try {
					css = Less.Parse(less, lessConfig);
				}
				catch {
					Console.WriteLine("Error while parsing LESS to CSS");
				}

				if (string.IsNullOrWhiteSpace(css))
					continue;

				compiledCSS += css;

				var path = automation.DestFile;

				if (string.IsNullOrWhiteSpace(path))
					continue;

				var pathIsAbs = path.Substring(1, 1) == ":" || path.StartsWith("\\");

				if (!pathIsAbs)
					path = Path.Combine(
						configPath,
						path);

				try {
					File.WriteAllText(
						path,
						css);
				}
				catch {
					Console.WriteLine("Could not write CSS to dest file {0}", automation.DestFile);
				}
			}
		}

		/// <summary>
		/// Get a list of all files matching the given pattern in the given folder.
		/// </summary>
		private static List<string> GetFiles(string path, string pattern = "*.*") {
			var files = new List<string>();
			var folders = new List<string> { path };
			var index = 0;

			while (true) {
				var folder = folders[index];

				try {
					var temp = Directory.GetDirectories(
						folder,
						"*",
						SearchOption.TopDirectoryOnly);

					folders.AddRange(temp);
				}
				catch {
					// ignore
				}

				foreach (var p in pattern.Split(';')) {
					try {
						var temp = Directory.GetFiles(
							folder,
							p,
							SearchOption.TopDirectoryOnly);

						files.AddRange(temp);
					}
					catch {
						// ignore
					}
				}

				index++;

				if (index == folders.Count)
					break;
			}

			return files;
		}

		/// <summary>
		/// Get the content of all given files, combined.
		/// </summary>
		private static string GetFileContents(List<string> files) {
			var content = new StringBuilder();

			foreach (var file in files) {
				try {
					content.Append(File.ReadAllText(file));
					content.AppendLine();
				}
				catch {
					// ignore
				}
			}

			return content.ToString();
		}
	}
}