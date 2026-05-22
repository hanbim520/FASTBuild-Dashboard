using System.Globalization;

namespace FastBuild.Dashboard.Communication.Events
{
	internal class ReportProgressEventArgs : BuildEventArgs
	{
		public const string ReportProgressEventName = "PROGRESS_STATUS";

		public static ReportProgressEventArgs Parse(string[] tokens)
		{
			var args = new ReportProgressEventArgs();
			BuildEventArgs.ParseBase(tokens, args);
			args.Progress = float.Parse(tokens[EventArgStartIndex], CultureInfo.InvariantCulture);
			if (tokens.Length > EventArgStartIndex + 1 &&
				int.TryParse(tokens[EventArgStartIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalJobCount))
			{
				args.TotalJobCount = totalJobCount;
			}
			if (tokens.Length > EventArgStartIndex + 2 &&
				int.TryParse(tokens[EventArgStartIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var remainingJobCount))
			{
				args.RemainingJobCount = remainingJobCount;
			}
			return args;
		}

		public double Progress { get; private set; }
		public int? TotalJobCount { get; private set; }
		public int? RemainingJobCount { get; private set; }
	}
}
