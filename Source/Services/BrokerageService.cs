using System;
using System.IO;
using System.Linq;
using System.Timers;

namespace FastBuild.Dashboard.Services
{
	internal class BrokerageService : IBrokerageService
	{
		private const string DefaultBrokeragePath = @"\\172.26.144.83\share\FASTBuild\Brokerage";
		private static readonly string[] WorkerPoolRelativePaths =
		{
			@"main\22.windows",
			@"main\22",
			@"main\20.windows",
			@"main\20",
			@"main\19.windows",
			@"main\19",
			@"main\18.windows",
			@"main\18",
			@"main\17",
			@"main\16",
		};

		private string[] _workerNames;
		private bool _isUpdatingWorkers;

		public string[] WorkerNames
		{
			get => _workerNames;
			private set
			{
				var hasChanged = !_workerNames.SequenceEqual(value);
				_workerNames = value;

				if (hasChanged)
				{
					this.WorkerCountChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public string BrokeragePath
		{
			get => Environment.GetEnvironmentVariable("FASTBUILD_BROKERAGE_PATH", EnvironmentVariableTarget.Process)
				?? Environment.GetEnvironmentVariable("FASTBUILD_BROKERAGE_PATH", EnvironmentVariableTarget.User)
				?? Environment.GetEnvironmentVariable("FASTBUILD_BROKERAGE_PATH", EnvironmentVariableTarget.Machine)
				?? DefaultBrokeragePath;
			set
			{
				Environment.SetEnvironmentVariable("FASTBUILD_BROKERAGE_PATH", value, EnvironmentVariableTarget.Process);
				Environment.SetEnvironmentVariable("FASTBUILD_BROKERAGE_PATH", value, EnvironmentVariableTarget.User);
			}
		}

		public event EventHandler WorkerCountChanged;

		public BrokerageService()
		{
			_workerNames = new string[0];

			var checkTimer = new Timer(5000);
			checkTimer.Elapsed += this.CheckTimer_Elapsed;
			checkTimer.AutoReset = true;
			checkTimer.Enabled = true;
			this.UpdateWorkers();
		}

		private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e) => this.UpdateWorkers();

		private void UpdateWorkers()
		{
			if (_isUpdatingWorkers)
			{
				return;
			}

			_isUpdatingWorkers = true;

			try
			{
				var brokeragePath = this.BrokeragePath;
				if (string.IsNullOrEmpty(brokeragePath))
				{
					this.WorkerNames = new string[0];
					return;
				}

				this.WorkerNames = WorkerPoolRelativePaths
					.Select(relativePath => Path.Combine(brokeragePath, relativePath))
					.Where(Directory.Exists)
					.SelectMany(GetWorkerFiles)
					.Select(Path.GetFileName)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray();
			}
			finally
			{
				_isUpdatingWorkers = false;
			}
		}

		private static string[] GetWorkerFiles(string directory)
		{
			try
			{
				return Directory.GetFiles(directory);
			}
			catch (IOException)
			{
				return new string[0];
			}
			catch (UnauthorizedAccessException)
			{
				return new string[0];
			}
		}
	}
}
