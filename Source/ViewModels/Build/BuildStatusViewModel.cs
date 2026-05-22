using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using Caliburn.Micro;
using FastBuild.Dashboard.Support;

namespace FastBuild.Dashboard.ViewModels.Build
{
	internal sealed class BuildStatusViewModel : PropertyChangedBase, IMainPage
	{
		private const string AllFilter = "All";
		private const string ErrorsFilter = "Errors";
		private const string WarningsFilter = "Warnings";

		private readonly BuildWatcherViewModel _buildWatcher;
		private string _selectedLogFilter = AllFilter;
		private BuildLogEntryViewModel _selectedBuildLog;

		public string Icon => "ClipboardText";
		public string DisplayName => "Build Status";

		public BuildSessionViewModel CurrentSession => _buildWatcher.CurrentSession;
		public SimpleCommand CopySelectedBuildLogCommand { get; }
		public SimpleCommand CopyVisibleBuildLogsCommand { get; }

		public BindableCollection<string> LogFilters { get; } = new BindableCollection<string>
		{
			AllFilter,
			ErrorsFilter,
			WarningsFilter
		};

		public string SelectedLogFilter
		{
			get => _selectedLogFilter;
			set
			{
				if (value == _selectedLogFilter)
				{
					return;
				}

				_selectedLogFilter = value;
				this.NotifyOfPropertyChange();
				this.RefreshVisibleBuildLogs();
			}
		}

		public BindableCollection<BuildLogEntryViewModel> VisibleBuildLogs { get; }
			= new BindableCollection<BuildLogEntryViewModel>();

		public BuildLogEntryViewModel SelectedBuildLog
		{
			get => _selectedBuildLog;
			set
			{
				if (ReferenceEquals(value, _selectedBuildLog))
				{
					return;
				}

				_selectedBuildLog = value;
				this.NotifyOfPropertyChange();
				this.CopySelectedBuildLogCommand.OnCanExecuteChanged();
			}
		}

		public int TotalLogCount => _buildWatcher.BuildLogEntries.Count;
		public int ErrorLogCount => _buildWatcher.BuildLogEntries.Count(l => l.IsError);
		public int WarningLogCount => _buildWatcher.BuildLogEntries.Count(l => l.IsWarning);
		public bool HasVisibleLogs => this.VisibleBuildLogs.Count > 0;

		public BuildStatusViewModel(BuildWatcherViewModel buildWatcher)
		{
			_buildWatcher = buildWatcher;
			this.CopySelectedBuildLogCommand = new SimpleCommand(_ => this.CopySelectedBuildLog(), _ => this.SelectedBuildLog != null);
			this.CopyVisibleBuildLogsCommand = new SimpleCommand(_ => this.CopyVisibleBuildLogs(), _ => this.VisibleBuildLogs.Count > 0);
			_buildWatcher.PropertyChanged += this.BuildWatcher_PropertyChanged;
			_buildWatcher.BuildLogEntries.CollectionChanged += this.BuildLogEntries_CollectionChanged;
			this.RefreshVisibleBuildLogs();
			this.NotifyLogCountsChanged();
		}

		public void CopySelectedBuildLog()
		{
			if (this.SelectedBuildLog == null)
			{
				return;
			}

			Clipboard.SetText(FormatLogEntry(this.SelectedBuildLog));
		}

		public void CopyVisibleBuildLogs()
		{
			if (this.VisibleBuildLogs.Count == 0)
			{
				return;
			}

			var text = string.Join(Environment.NewLine, this.VisibleBuildLogs.Select(FormatLogEntry));
			Clipboard.SetText(text);
		}

		private static string FormatLogEntry(BuildLogEntryViewModel entry)
		{
			var builder = new StringBuilder();
			builder.Append(entry.DisplayTime).Append('\t');
			builder.Append(entry.Level).Append('\t');
			builder.Append(entry.ResultText).Append('\t');
			builder.Append(entry.HostName).Append('\t');
			builder.Append(entry.EventName);

			if (!string.IsNullOrEmpty(entry.Message))
			{
				builder.Append('\t').Append(entry.Message);
			}

			return builder.ToString();
		}

		private void BuildWatcher_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(BuildWatcherViewModel.CurrentSession))
			{
				this.NotifyOfPropertyChange(nameof(this.CurrentSession));
			}
		}

		private void BuildLogEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
			{
				foreach (BuildLogEntryViewModel entry in e.NewItems)
				{
					if (this.IsVisible(entry))
					{
						this.VisibleBuildLogs.Add(entry);
					}
				}
			}
			else
			{
				this.RefreshVisibleBuildLogs();
			}

			this.NotifyLogCountsChanged();
			this.CopyVisibleBuildLogsCommand.OnCanExecuteChanged();
		}

		private bool IsVisible(BuildLogEntryViewModel entry)
		{
			switch (this.SelectedLogFilter)
			{
				case ErrorsFilter:
					return entry.IsError;
				case WarningsFilter:
					return entry.IsWarning;
				default:
					return true;
			}
		}

		private void RefreshVisibleBuildLogs()
		{
			this.VisibleBuildLogs.Clear();
			this.VisibleBuildLogs.AddRange(_buildWatcher.BuildLogEntries.Where(this.IsVisible));
			this.NotifyOfPropertyChange(nameof(this.HasVisibleLogs));
			this.CopyVisibleBuildLogsCommand.OnCanExecuteChanged();
		}

		private void NotifyLogCountsChanged()
		{
			this.NotifyOfPropertyChange(nameof(this.TotalLogCount));
			this.NotifyOfPropertyChange(nameof(this.ErrorLogCount));
			this.NotifyOfPropertyChange(nameof(this.WarningLogCount));
			this.NotifyOfPropertyChange(nameof(this.HasVisibleLogs));
		}
	}
}
