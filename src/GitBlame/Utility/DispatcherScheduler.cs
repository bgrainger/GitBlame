using System;
using System.Reactive.Concurrency;
using System.Windows.Threading;

namespace GitBlame.Utility
{
	internal sealed class DispatcherScheduler : IScheduler
	{
		public DispatcherScheduler() => m_dispatcher = Dispatcher.CurrentDispatcher;

		public DateTimeOffset Now => DateTimeOffset.Now;

		public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
		{
			var operation = m_dispatcher.BeginInvoke(action, this, state);
			return new CancellableOperation(operation);
		}

		public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action) => throw new NotImplementedException();

		public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) => throw new NotImplementedException();

		private class CancellableOperation : IDisposable
		{
			public CancellableOperation(DispatcherOperation operation) => m_operation = operation;

			public void Dispose() => m_operation.Abort();

			private readonly DispatcherOperation m_operation;
		}

		private readonly Dispatcher m_dispatcher;
	}
}
