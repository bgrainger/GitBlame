using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows;

namespace GitBlame.Utility
{
	/// <summary>
	/// Provides helper methods for working with <see cref="DependencyProperty"/>.
	/// </summary>
	public static class DependencyPropertyUtility
	{
		/// <summary>
		/// Registers the specified property.
		/// </summary>
		/// <typeparam name="TProperty">The type of the property.</typeparam>
		/// <typeparam name="TOwner">The type of the owner.</typeparam>
		/// <param name="name">The name of the dependency property to register.</param>
		public static DependencyProperty Register<TProperty, TOwner>(string name)
			where TOwner : DependencyObject
		{
			return DependencyProperty.Register(name, typeof(TProperty), typeof(TOwner));
		}

		/// <summary>
		/// Creates an <see cref="IObservable{T}"/> from a <see cref="DependencyProperty"/>.
		/// </summary>
		/// <typeparam name="T">The type of value stored in the dependency property.</typeparam>
		/// <param name="dependencyObject">The <see cref="DependencyObject"/> whose dependency property will be observed.</param>
		/// <param name="property">The <see cref="DependencyProperty"/> to observe.</param>
		/// <returns>An <see cref="IObservable{T}"/> that observes the specified <see cref="DependencyProperty"/> on <paramref name="dependencyObject"/>.</returns>
		public static IObservable<T> ToObservable<T>(this DependencyObject dependencyObject, DependencyProperty property)
		{
			return Observable.Create<T>(o =>
			{
				var descriptor = DependencyPropertyDescriptor.FromProperty(property, dependencyObject.GetType());
				var eventHandler = new EventHandler((s, e) => o.OnNext((T) descriptor.GetValue(dependencyObject)));
				descriptor.AddValueChanged(dependencyObject, eventHandler);
				return () => descriptor.RemoveValueChanged(dependencyObject, eventHandler);
			});
		}

		/// <summary>
		/// Creates an <see cref="IObservable{T}"/> from a <see cref="DependencyProperty"/>.
		/// </summary>
		/// <typeparam name="T">The type of value stored in the dependency property.</typeparam>
		/// <param name="dependencyObject">The <see cref="DependencyObject"/> whose dependency property will be observed.</param>
		/// <param name="property">The <see cref="DependencyProperty"/> to observe.</param>
		/// <returns>An <see cref="IObservable{T}"/> that observes the specified <see cref="DependencyProperty"/> on <paramref name="dependencyObject"/>.</returns>
		/// <remarks>The returned observable will have the current value of <paramref name="property"/> as its initial value.</remarks>
		public static IObservable<T> ToObservableWithInitialValue<T>(this DependencyObject dependencyObject, DependencyProperty property)
		{
			return Observable.Create<T>(o =>
			{
				var descriptor = DependencyPropertyDescriptor.FromProperty(property, dependencyObject.GetType());
				var eventHandler = new EventHandler((s, e) => o.OnNext((T) descriptor.GetValue(dependencyObject)));
				descriptor.AddValueChanged(dependencyObject, eventHandler);
				o.OnNext((T) descriptor.GetValue(dependencyObject));
				return () => descriptor.RemoveValueChanged(dependencyObject, eventHandler);
			});
		}
	}
}
