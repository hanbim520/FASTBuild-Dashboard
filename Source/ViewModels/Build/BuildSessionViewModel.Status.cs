using System;
using Caliburn.Micro;
using FastBuild.Dashboard.Services;

namespace FastBuild.Dashboard.ViewModels.Build
{
	partial class BuildSessionViewModel
	{
		private bool _isRunning;
		private double _progress;
		private bool _isRestoringHistory;
		private int _inProgressJobCount;
		private int _totalJobCount;
		private int _remainingJobCount;
		private int _finishedJobCount;
		private bool _hasProgressTotal;
		private bool _hasProgressRemaining;
		private int _successfulJobCount;
		private int _preprocessedJobCount;
		private int _failedJobCount;
		private int _failedCompilationJobCount;
		private int _warningJobCount;
		private int _cacheHitCount;
		private int _activeWorkerCount;
		private int _activeCoreCount;
		private string[] _poolWorkerNames;

		public bool IsRestoringHistory
		{
			get => _isRestoringHistory;
			set
			{
				if (value == _isRestoringHistory)
				{
					return;
				}

				_isRestoringHistory = value;
				this.NotifyOfPropertyChange();
				this.NotifyOfPropertyChange(nameof(this.IsSessionViewVisible));
				this.NotifyOfPropertyChange(nameof(this.StatusText));

				if (!this.IsRestoringHistory)
				{
					// refresh these values after restoring history because they are not updated during the process
					// in order to increase history restoration performance
					this.NotifyOfPropertyChange(nameof(this.SuccessfulJobCount));
					this.NotifyOfPropertyChange(nameof(this.PreprocessedJobCount));
					this.NotifyOfPropertyChange(nameof(this.CacheHitCount));
					this.NotifyOfPropertyChange(nameof(this.InProgressJobCount));
					this.NotifyOfPropertyChange(nameof(this.FailedJobCount));
					this.NotifyOfPropertyChange(nameof(this.WarningJobCount));
					this.NotifyOfPropertyChange(nameof(this.TotalJobCount));
					this.NotifyOfPropertyChange(nameof(this.RemainingJobCount));
					this.NotifyJobProgressChanged();

					this.DetectDebris();
				}
			}
		}

		public bool IsSessionViewVisible => !this.IsRestoringHistory;

		public bool IsRunning
		{
			get => _isRunning;
			private set
			{
				if (value == _isRunning)
				{
					return;
				}

				_isRunning = value;
				this.NotifyOfPropertyChange();
				this.NotifyOfPropertyChange(nameof(this.StatusText));
				this.NotifyOfPropertyChange(nameof(this.RemainingJobCount));
			}
		}

		public double Progress
		{
			get => _progress;
			private set
			{
				if (value.Equals(_progress))
				{
					return;
				}

				_progress = value;
				this.NotifyOfPropertyChange();
				this.NotifyJobProgressChanged();
				if (this.IsRunning)
				{
					this.NotifyOfPropertyChange(nameof(this.StatusText));
				}
			}
		}

		public string StatusText
		{
			get
			{
				if (_isRestoringHistory)
				{
					return $"Loading ({this.Progress:0}%)";
				}

				if (this.IsRunning)
				{
					return $"Building ({this.JobProgressPercent:0}%)";
				}

				return "Finished";
			}
		}

		public int InProgressJobCount
		{
			get
			{
				if (_hasProgressTotal)
				{
					return Math.Min(_inProgressJobCount, this.RemainingJobCount);
				}

				return _inProgressJobCount;
			}
			private set
			{
				if (value == _inProgressJobCount)
				{
					return;
				}

				_inProgressJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
				}
			}
		}

		public int TotalJobCount
		{
			get => _totalJobCount;
			private set
			{
				if (value == _totalJobCount)
				{
					return;
				}

				_totalJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
					this.NotifyJobProgressChanged();
				}
			}
		}

		public int RemainingJobCount
		{
			get => this.GetDisplayRemainingJobCount();
			private set
			{
				value = Math.Max(0, value);
				if (value == _remainingJobCount)
				{
					return;
				}

				_remainingJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
					this.NotifyJobProgressChanged();
				}
			}
		}

		public int BuiltJobCount
		{
			get
			{
				if (_hasProgressTotal)
				{
					return Math.Max(0, Math.Min(this.TotalJobCount, this.TotalJobCount - this.RemainingJobCount));
				}

				return _finishedJobCount;
			}
		}

		private int GetDisplayRemainingJobCount()
		{
			var remainingJobCount = Math.Max(0, _remainingJobCount);
			if (!_hasProgressTotal || this.TotalJobCount <= 0)
			{
				return remainingJobCount;
			}

			remainingJobCount = Math.Min(this.TotalJobCount, remainingJobCount);
			return remainingJobCount;
		}

		public double JobProgressPercent
		{
			get
			{
				if (_hasProgressTotal && this.TotalJobCount > 0)
				{
					return Math.Max(0, Math.Min(100, this.BuiltJobCount * 100.0 / this.TotalJobCount));
				}

				return this.Progress;
			}
		}

		public string BuildProgressText => $"{this.BuiltJobCount} of {this.TotalJobCount} built ({this.JobProgressPercent:0}%)";

		private void NotifyJobProgressChanged()
		{
			this.NotifyOfPropertyChange(nameof(this.RemainingJobCount));
			this.NotifyOfPropertyChange(nameof(this.BuiltJobCount));
			this.NotifyOfPropertyChange(nameof(this.JobProgressPercent));
			this.NotifyOfPropertyChange(nameof(this.BuildProgressText));
			this.NotifyOfPropertyChange(nameof(this.StatusText));
			this.NotifyOfPropertyChange(nameof(this.InProgressJobCount));
			this.NotifyOfPropertyChange(nameof(this.SuccessfulJobCount));
			this.NotifyOfPropertyChange(nameof(this.PreprocessedJobCount));
			this.NotifyOfPropertyChange(nameof(this.CacheHitCount));
			this.NotifyOfPropertyChange(nameof(this.FailedJobCount));
		}

		private void ApplyProgressJobCounts(int? totalJobCount, int? remainingJobCount)
		{
			if (!_hasProgressTotal && totalJobCount.HasValue && totalJobCount.Value > 0)
			{
				_hasProgressTotal = true;
				this.TotalJobCount = totalJobCount.Value;
			}

			if (_hasProgressTotal)
			{
				if (remainingJobCount.HasValue)
				{
					_hasProgressRemaining = true;
					this.RemainingJobCount = Math.Min(this.TotalJobCount, remainingJobCount.Value);
				}
				else if (!_hasProgressRemaining)
				{
					this.RemainingJobCount = this.TotalJobCount - _finishedJobCount;
				}
			}

			this.NotifyJobProgressChanged();
		}

		private void TrackFinishedJob()
		{
			++_finishedJobCount;
			if (_hasProgressTotal && !_hasProgressRemaining)
			{
				this.RemainingJobCount = this.TotalJobCount - _finishedJobCount;
			}
			else if (!_hasProgressTotal)
			{
				this.NotifyJobProgressChanged();
			}
		}

		private void CompleteJobProgress()
		{
			if (!_hasProgressTotal && this.TotalJobCount < _finishedJobCount)
			{
				this.TotalJobCount = _finishedJobCount;
			}

			this.RemainingJobCount = 0;
			_hasProgressRemaining = _hasProgressTotal;
		}

		public int SuccessfulJobCount
		{
			get
			{
				if (_hasProgressTotal)
				{
					return Math.Max(0, this.BuiltJobCount - _failedCompilationJobCount);
				}

				return _successfulJobCount;
			}
			private set
			{
				if (value == _successfulJobCount)
				{
					return;
				}

				_successfulJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
					this.NotifyOfPropertyChange(nameof(this.CacheHitCount));
				}
			}
		}

		public int PreprocessedJobCount
		{
			get => this.ClampToProgressTotal(_preprocessedJobCount);
			private set
			{
				if (value == _preprocessedJobCount)
				{
					return;
				}

				_preprocessedJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
				}
			}
		}

		public int FailedJobCount
		{
			get => _failedJobCount;
			private set
			{
				if (value == _failedJobCount)
				{
					return;
				}

				_failedJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
					this.NotifyOfPropertyChange(nameof(this.SuccessfulJobCount));
					this.NotifyOfPropertyChange(nameof(this.CacheHitCount));
				}
			}
		}

		public int WarningJobCount
		{
			get => _warningJobCount;
			private set
			{
				if (value == _warningJobCount)
				{
					return;
				}

				_warningJobCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
				}
			}
		}

		public int CacheHitCount
		{
			get => this.ClampToProgressTotal(_cacheHitCount);
			private set
			{
				if (value == _cacheHitCount)
				{
					return;
				}

				_cacheHitCount = value;

				if (!this.IsRestoringHistory)
				{
					this.NotifyOfPropertyChange();
				}
			}
		}

		private int ClampToProgressTotal(int value)
		{
			if (!_hasProgressTotal || this.TotalJobCount <= 0)
			{
				return Math.Max(0, value);
			}

			return Math.Max(0, Math.Min(value, this.TotalJobCount));
		}

		public int ActiveWorkerCount
		{
			get => _activeWorkerCount;
			private set
			{
				if (value == _activeWorkerCount)
				{
					return;
				}

				_activeWorkerCount = value;
				this.NotifyOfPropertyChange();
			}
		}

		public int ActiveCoreCount
		{
			get => _activeCoreCount;
			private set
			{
				if (value == _activeCoreCount)
				{
					return;
				}

				_activeCoreCount = value;
				this.NotifyOfPropertyChange();
			}
		}

		public int PoolWorkerCount => this.PoolWorkerNames.Length;

		public string[] PoolWorkerNames
		{
			get => _poolWorkerNames;
			private set
			{
				_poolWorkerNames = value;
				this.NotifyOfPropertyChange();
				this.NotifyOfPropertyChange(nameof(this.PoolWorkerCount));
			}
		}

		private void UpdateActiveWorkerAndCoreCount()
		{
			if (this.IsRestoringHistory)
			{
				return;
			}

			var activeWorkerCount = 0;
			var activeCoreCount = 0;
			foreach (var worker in this.Workers)
			{
				if (worker.ActiveCoreCount > 0)
				{
					++activeWorkerCount;
					activeCoreCount += worker.ActiveCoreCount;
				}
			}

			this.ActiveWorkerCount = activeWorkerCount;
			this.ActiveCoreCount = activeCoreCount;
		}


		private void BrokerageService_WorkerCountChanged(object sender, EventArgs e)
		{
			this.PoolWorkerNames = IoC.Get<IBrokerageService>().WorkerNames;
		}
	}
}
