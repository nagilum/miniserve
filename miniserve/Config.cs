using System;
using System.Collections.Generic;

namespace miniserve {
	public class Config {
		public int Port { get; set; }
		public string Path { get; set; }
		public List<ConfigAutomation> Automations { get; set; }
	}

	public class ConfigAutomation {
		public string TagName { get; set; }
		public string Type { get; set; }
		public bool Minify { get; set; }
		public List<string> SourceFiles { get; set; }
		public bool ParseTags { get; set; }
		public string DestFile { get; set; }
		public int WaitBeforeParsing { get; set; }
		public List<ConfigAutomationFSW> fswEntries { get; set; } 
	}

	public class ConfigAutomationFSW {
		public string Path { get; set; }
		public string Pattern { get; set; }
	}

	public class CompiledContent {
		public string TagName { get; set; }
		public string Type { get; set; }
		public string Content { get; set; }
		public DateTime LastCompile { get; set; }
	}
}