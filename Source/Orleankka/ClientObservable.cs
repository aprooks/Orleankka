﻿using System;

namespace Orleankka
{
    using Utility;

    public interface IClientObservable : IObservable<object>, IDisposable
    {
        ClientRef Ref { get; }
    }

    /// <summary>
    /// Allows clients to receive push-based notifications from actors, ie observing them.
    /// <para>
    /// To teardown created back-channel and delete underlying client endpoint, call <see cref="IDisposable.Dispose"/>
    /// </para>
    /// </summary>
    /// <remarks> Instances of this type are not thread safe </remarks>
    sealed class ClientObservable : IClientObservable
    {
        readonly ClientEndpoint endpoint;

        ClientObservable(ClientRef @ref)
        {
            Ref = @ref;
        }

        internal ClientObservable(ClientEndpoint endpoint) 
            : this(endpoint.Self)
        {
            this.endpoint = endpoint;
        }

        public ClientRef Ref {get; }

        public IDisposable Subscribe(IObserver<object> observer) => 
            endpoint.Subscribe(observer);

        public void Dispose() => 
            endpoint.Dispose();
    }

    public static class ClientObservableExtensions
    {
        /// <summary>
        ///   Notifies the provider that an observer is to receive notifications.
        /// </summary>
        /// <returns>
        ///   A reference to an interface that allows observers to stop receiving notifications before the provider has finished
        ///   sending them.
        /// </returns>
        /// <param name="observable">The instance of client observable proxy</param>
        /// <param name="callback">The callback delegate that is to receive notifications</param>
        public static IDisposable Subscribe(this IClientObservable observable, Action<object> callback)
        {
            Requires.NotNull(callback, nameof(callback));

            return observable.Subscribe(new DelegateObserver(callback));
        }

        public static IDisposable Subscribe<T>(this IClientObservable observable, Action<T> callback)
        {
            Requires.NotNull(callback, nameof(callback));

            return observable.Subscribe(new DelegateObserver(x => callback((T)x)));
        }

        class DelegateObserver : IObserver<object>
        {
            readonly Action<object> callback;

            public DelegateObserver(Action<object> callback)
            {
                this.callback = callback;
            }

            public void OnNext(object value)
            {
                callback(value);
            }

            public void OnError(Exception error)
            {
                throw new NotImplementedException();
            }

            public void OnCompleted()
            {
                throw new NotImplementedException();
            }
        }
    }
}