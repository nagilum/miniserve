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
		/// Compiled CSS and JS.
		/// </summary>
		public static List<CompiledContent> compiledContent = new List<CompiledContent>();

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
				// Set tag names
				foreach (var automation in config.Automations)
					if (string.IsNullOrWhiteSpace(automation.TagName))
						automation.TagName = Guid.NewGuid().ToString();

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
			    var af = new List<string>();

				foreach (var entry in automation.fswEntries) {
					var files = GetFiles(
						entry.Path,
						entry.Pattern);

				    foreach (var file in files) {
				        if (af.Contains(file)) {
				            continue;
				        }

				        af.Add(file);
				    }
				}

			    html += GetFileContents(af);

				if (automation.ParseTags)
					html = ReplaceTags(html);

				if (automation.Minify) {
					var lines = html.Split('\r');

					if (lines.Length == 1)
						lines = html.Split('\n');

					html = lines.Aggregate(string.Empty, (current, line) => current + line.Trim());
				}

				var cc = compiledContent
					.SingleOrDefault(c => c.TagName == automation.TagName);

				if (cc == null) {
					cc = new CompiledContent {
						TagName = automation.TagName,
						Type = automation.Type
					};

					compiledContent.Add(cc);
				}

				cc.Content = html;
				cc.LastCompile = DateTime.Now;

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

			foreach (var automation in config.Automations.Where(a => a.Type.ToLower() == "js")) {
				if (automation.WaitBeforeParsing > 0)
					Thread.Sleep(automation.WaitBeforeParsing);

				if (automation.fswEntries == null)
					continue;

				var js = string.Empty;
			    var af = new List<string>();

				foreach (var entry in automation.fswEntries) {
					var files = GetFiles(
						entry.Path,
						entry.Pattern);

				    foreach (var file in files) {
				        if (af.Contains(file)) {
				            continue;
				        }

				        af.Add(file);
				    }
				}

			    js += GetFileContents(af);

				if (automation.ParseTags)
					js = ReplaceTags(js);

				try {
					if (automation.Minify)
						js = new JavaScriptMinifier().Minify(js);
				}
				catch {
					Console.WriteLine("Could not minify JS");
				}

				var cc = compiledContent
					.SingleOrDefault(c => c.TagName == automation.TagName);

				if (cc == null) {
					cc = new CompiledContent {
						TagName = automation.TagName,
						Type = automation.Type
					};

					compiledContent.Add(cc);
				}

				cc.Content = js;
				cc.LastCompile = DateTime.Now;

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

			foreach (var automation in config.Automations.Where(a => a.Type.ToLower() == "less")) {
				if (automation.WaitBeforeParsing > 0)
					Thread.Sleep(automation.WaitBeforeParsing);

				if (automation.fswEntries == null)
					continue;

				var less = string.Empty;
				var css = string.Empty;
			    var af = new List<string>();

				foreach (var entry in automation.fswEntries) {
					var files = GetFiles(
						entry.Path,
						entry.Pattern);

				    foreach (var file in files) {
				        if (af.Contains(file)) {
				            continue;
				        }

				        af.Add(file);
				    }
				}

			    less += GetFileContents(af);

				if (automation.ParseTags)
					less = ReplaceTags(less);

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

				var cc = compiledContent
					.SingleOrDefault(c => c.TagName == automation.TagName);

				if (cc == null) {
					cc = new CompiledContent {
						TagName = automation.TagName,
						Type = automation.Type
					};

					compiledContent.Add(cc);
				}

				cc.Content = css;
				cc.LastCompile = DateTime.Now;

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
		private static IEnumerable<string> GetFiles(string path, string pattern = "*.*") {
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
		private static string GetFileContents(IEnumerable<string> files) {
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

		/// <summary>
		/// Cycle content and replace tags.
		/// </summary>
		private static string ReplaceTags(string content) {
			var tags = new List<string>();
			var sp = 0;

			while (true) {
				sp = content.IndexOf("{{", sp, StringComparison.InvariantCultureIgnoreCase);

				if (sp == -1)
					break;

				var ep = content.IndexOf("}}", sp, StringComparison.InvariantCultureIgnoreCase);

				if (ep == -1)
					break;

				var tag = content
					.Substring(sp, (ep - sp) + 2);

				if (!tags.Contains(tag))
					tags.Add(tag);

				sp++;
			}

			foreach (var tag in tags) {
				var tagName = tag
					.Substring(2)
					.Substring(0, tag.Length - 4)
					.Trim();

				var cc = compiledContent
					.SingleOrDefault(c => c.TagName == tagName);

				if (cc == null)
					continue;

				var temp = string.Empty;

				if (cc.Type == "html") temp = cc.Content;
				if (cc.Type == "js") temp = string.Format("<script>{0}</script>", cc.Content);
				if (cc.Type == "less") temp = string.Format("<style type=\"text/css\">{0}</style>", cc.Content);

				if (string.IsNullOrWhiteSpace(temp))
					continue;

				content = content
					.Replace(
						tag,
						temp);
			}

			return content;
		}
	}
}