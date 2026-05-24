using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastBuild.Dashboard.Communication
{
	internal class LogWatcher
	{
		private const string LogRelativePath = @"FASTBuild\FastBuildLog.log";
		private const int MaxReadBufferLength = 64 * 1024;

		private string _logPath;

		public event EventHandler HistoryRestorationStarted;
		public event EventHandler HistoryRestorationEnded;

		public event EventHandler LogReset;
		public event EventHandler<string> LogReceived;

		private long _fileStreamPosition;
		private DateTime _currentFileTime;
		private readonly List<byte> _messageBuffer = new List<byte>();

		public bool IsRestoringHistory { get; private set; }

		public LogWatcher()
		{
#if DEBUG && DEBUG_TEST_LOG
			_logPath = @"Test\FastBuildLog.log";
#else
			_logPath = this.FindLogPath();
#endif
		}

		public void Start()
		{
			this.EnsureLogDirectory();

			if (File.Exists(_logPath))
			{
				this.IsRestoringHistory = true;
				this.HistoryRestorationStarted?.Invoke(this, EventArgs.Empty);
			}

			Task.Factory.StartNew(this.ReadRemainingLogsAsync);
		}

		private void ReadRemainingLogsAsync()
		{
			_fileStreamPosition = 0;
			while (true)
			{
				try
				{
					this.ReadRemainingLogs();
				}
				catch (Exception ex) when (IsRecoverableLogAccessException(ex))
				{
				}

				Thread.Sleep(500);
			}
		}

		private void ReadRemainingLogs()
		{
			this.UpdateLogPath();

			if (File.Exists(_logPath))
			{
				var fileInfo = new FileInfo(_logPath);
				var fileTime = fileInfo.LastWriteTime;
				if (fileInfo.Length < _fileStreamPosition)
				{
					_fileStreamPosition = 0;
					_messageBuffer.Clear();
					this.LogReset?.Invoke(this, EventArgs.Empty);
				}

				_currentFileTime = fileTime;

				using (var file = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				{
					var expectedLength = file.Length - _fileStreamPosition;
					if (expectedLength > 0)
					{
						file.Seek(_fileStreamPosition, SeekOrigin.Begin);

						var buffer = new byte[Math.Min(MaxReadBufferLength, expectedLength)];
						while (expectedLength > 0)
						{
							var readLength = file.Read(buffer, 0, (int)Math.Min(buffer.Length, expectedLength));
							if (readLength == 0)
							{
								break;
							}

							_fileStreamPosition += readLength;
							expectedLength -= readLength;

							for (var i = 0; i < readLength; ++i)
							{
								this.ReadLogByte(buffer[i]);
							}
						}
					}
				}

				if (this.IsRestoringHistory)
				{
					this.IsRestoringHistory = false;
					this.HistoryRestorationEnded?.Invoke(this, EventArgs.Empty);
				}
			}
			else
			{
				_fileStreamPosition = 0;
			}
		}

		private void ReadLogByte(byte c)
		{
			if (c == '\n')
			{
				this.FlushMessage();
				return;
			}

			_messageBuffer.Add(c);
		}

		private static bool IsRecoverableLogAccessException(Exception ex)
		{
			return ex is IOException
				|| ex is UnauthorizedAccessException
				|| ex is ArgumentException
				|| ex is NotSupportedException
				|| ex is System.Security.SecurityException;
		}

		private void UpdateLogPath()
		{
#if !(DEBUG && DEBUG_TEST_LOG)
			var logPath = this.FindLogPath();
			if (string.Equals(logPath, _logPath, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			_logPath = logPath;
			_fileStreamPosition = 0;
			_currentFileTime = DateTime.MinValue;
			_messageBuffer.Clear();
			this.EnsureLogDirectory();
			this.LogReset?.Invoke(this, EventArgs.Empty);

			if (File.Exists(_logPath))
			{
				this.IsRestoringHistory = true;
				this.HistoryRestorationStarted?.Invoke(this, EventArgs.Empty);
			}
#endif
		}

		private string FindLogPath()
		{
			var fastbuildTempPath = Environment.GetEnvironmentVariable("FASTBUILD_TEMP_PATH");
			if (fastbuildTempPath != null && Directory.Exists(fastbuildTempPath))
			{
				return Path.Combine(fastbuildTempPath, LogRelativePath);
			}

			var selectedPath = Path.Combine(Path.GetTempPath(), LogRelativePath);
			var selectedTime = File.Exists(selectedPath) ? File.GetLastWriteTime(selectedPath) : DateTime.MinValue;

			// UnrealBuildTool overrides TEMP for child processes, so UE builds write the monitor log below this folder.
			var unrealBuildToolTempPath = Path.Combine(Path.GetTempPath(), "UnrealBuildTool");
			if (!Directory.Exists(unrealBuildToolTempPath))
			{
				return selectedPath;
			}

			try
			{
				foreach (var directory in Directory.GetDirectories(unrealBuildToolTempPath))
				{
					var candidatePath = Path.Combine(directory, LogRelativePath);
					if (!File.Exists(candidatePath))
					{
						continue;
					}

					var candidateTime = File.GetLastWriteTime(candidatePath);
					if (candidateTime > selectedTime)
					{
						selectedPath = candidatePath;
						selectedTime = candidateTime;
					}
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return selectedPath;
		}

		private void EnsureLogDirectory()
		{
			var logDirectory = Path.GetDirectoryName(_logPath);
			if (!Directory.Exists(logDirectory))
			{
				Debug.Assert(logDirectory != null, "logDirectory != null");
				Directory.CreateDirectory(logDirectory);
			}
		}

		private void FlushMessage()
		{
			if (_messageBuffer.Count > 0)
			{
				var message = Encoding.Default.GetString(_messageBuffer.ToArray(), 0, _messageBuffer.Count);
				this.LogReceived?.Invoke(this, message);
				_messageBuffer.Clear();
			}
		}
	}
}
