﻿namespace IronPigeon {
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Validation;
#if !NET40
	using TaskEx = System.Threading.Tasks.Task;
#endif

	/// <summary>
	/// An incremental progress reporting mechanism that also allows
	/// asynchronous awaiting for all reports to be processed.
	/// </summary>
	/// <typeparam name="T">The type of message sent in progress updates.</typeparam>
	public class ProgressWithCompletion<T> : IProgress<T> {
		/// <summary>
		/// The synchronization object.
		/// </summary>
		private readonly object syncObject = new object();

		/// <summary>
		/// The handler to invoke for each progress update.
		/// </summary>
		private readonly Func<T, Task> handler;

		/// <summary>
		/// The set of progress reports that have started (but may not have finished yet).
		/// </summary>
		private readonly HashSet<Task> outstandingTasks = new HashSet<Task>();

		/// <summary>
		/// The factory to use for spawning reports.
		/// </summary>
		private readonly TaskFactory taskFactory =
			new TaskFactory(SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Default);

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgressWithCompletion{T}" /> class.
		/// </summary>
		/// <param name="handler">The handler.</param>
		public ProgressWithCompletion(Action<T> handler) {
			Requires.NotNull(handler, "handler");
			this.handler = value => {
				handler(value);
				return TaskEx.FromResult<object>(null);
			};
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgressWithCompletion{T}" /> class.
		/// </summary>
		/// <param name="handler">The async handler.</param>
		public ProgressWithCompletion(Func<T, Task> handler) {
			Requires.NotNull(handler, "handler");
			this.handler = handler;
		}

		/// <summary>
		/// Receives a progress update.
		/// </summary>
		/// <param name="value">The value representing the updated progress.</param>
		void IProgress<T>.Report(T value) {
			var reported = this.taskFactory.StartNew(() => this.handler(value)).Unwrap();
			lock (this.syncObject) {
				this.outstandingTasks.Add(reported);
			}

			reported.ContinueWith(
				t => {
					lock (this.syncObject) {
						this.outstandingTasks.Remove(t);
					}
				},
				CancellationToken.None,
				TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
		}

		/// <summary>
		/// Returns a task that completes when all reported progress has executed.
		/// </summary>
		/// <returns>A task that completes when all progress is complete.</returns>
		public Task WaitAsync() {
			lock (this.syncObject) {
				return TaskEx.WhenAll(this.outstandingTasks);
			}
		}
	}
}
