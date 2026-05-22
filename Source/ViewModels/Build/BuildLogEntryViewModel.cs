using System;
using System.IO;
using System.Text.RegularExpressions;
using FastBuild.Dashboard.Communication;
using FastBuild.Dashboard.Communication.Events;

namespace FastBuild.Dashboard.ViewModels.Build
{
	internal sealed class BuildLogEntryViewModel
	{
		private static readonly Regex WarningRegex = new Regex(
			@"(^|[\s:])warning\b|warning\s+[A-Z]+\d+",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

		public static bool ContainsCompilerWarning(string message)
			=> !string.IsNullOrWhiteSpace(message) && WarningRegex.IsMatch(message);

		private static string GetDisplayName(string eventName)
		{
			try
			{
				return Path.GetFileName(eventName) ?? eventName;
			}
			catch (ArgumentException)
			{
				return eventName;
			}
		}

		private static string NormalizeMessage(string message)
		{
			return string.IsNullOrWhiteSpace(message)
				? string.Empty
				: message.TrimEnd().Replace('\f', '\n');
		}

		private static bool GetIsError(BuildJobStatus result)
		{
			switch (result)
			{
				case BuildJobStatus.Failed:
				case BuildJobStatus.Error:
				case BuildJobStatus.Timeout:
				case BuildJobStatus.Aborted:
					return true;
				default:
					return false;
			}
		}

		public BuildLogEntryViewModel(FinishJobEventArgs e)
		{
			this.Time = e.Time;
			this.HostName = e.HostName;
			this.EventName = e.EventName;
			this.DisplayName = GetDisplayName(e.EventName);
			this.Result = e.Result;
			this.Message = NormalizeMessage(e.Message);
			this.IsError = GetIsError(e.Result);
			this.IsWarning = !this.IsError && ContainsCompilerWarning(this.Message);
		}

		public DateTime Time { get; }
		public string DisplayTime => this.Time.ToString("HH:mm:ss");
		public string HostName { get; }
		public string EventName { get; }
		public string DisplayName { get; }
		public BuildJobStatus Result { get; }
		public string ResultText => this.Result.ToString();
		public string Message { get; }
		public bool HasMessage => !string.IsNullOrEmpty(this.Message);
		public bool IsError { get; }
		public bool IsWarning { get; }
		public bool IsNormal => !this.IsError && !this.IsWarning;
		public string Level => this.IsError ? "Error" : this.IsWarning ? "Warning" : "Task";
		public string Icon => this.IsError ? "CloseOctagonOutline" : this.IsWarning ? "AlertOutline" : "CheckboxMarkedCircleOutline";
	}
}
