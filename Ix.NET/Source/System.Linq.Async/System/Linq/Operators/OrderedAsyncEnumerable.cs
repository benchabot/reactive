﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.Linq
{
    internal abstract class OrderedAsyncEnumerable<TElement> : AsyncIterator<TElement>, IOrderedAsyncEnumerable<TElement>
    {
        internal IOrderedEnumerable<TElement> enumerable;
        internal IAsyncEnumerable<TElement> source;

        IOrderedAsyncEnumerable<TElement> IOrderedAsyncEnumerable<TElement>.CreateOrderedEnumerable<TKey>(Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending)
        {
            return new OrderedAsyncEnumerable<TElement, TKey>(source, keySelector, comparer, descending, this);
        }

        IOrderedAsyncEnumerable<TElement> IOrderedAsyncEnumerable<TElement>.CreateOrderedEnumerable<TKey>(Func<TElement, Task<TKey>> keySelector, IComparer<TKey> comparer, bool descending)
        {
            return new OrderedAsyncEnumerableWithTask<TElement, TKey>(source, keySelector, comparer, descending, this);
        }

        internal abstract Task Initialize(CancellationToken cancellationToken);
    }

    internal sealed class OrderedAsyncEnumerable<TElement, TKey> : OrderedAsyncEnumerable<TElement>
    {
        private readonly IComparer<TKey> _comparer;
        private readonly bool _descending;
        private readonly Func<TElement, TKey> _keySelector;
        private readonly OrderedAsyncEnumerable<TElement> _parent;

        private IEnumerator<TElement> _enumerator;
        private IAsyncEnumerator<TElement> _parentEnumerator;

        public OrderedAsyncEnumerable(IAsyncEnumerable<TElement> source, Func<TElement, TKey> keySelector, IComparer<TKey> comparer, bool descending, OrderedAsyncEnumerable<TElement> parent)
        {
            Debug.Assert(source != null);
            Debug.Assert(keySelector != null);
            Debug.Assert(comparer != null);

            this.source = source;
            _keySelector = keySelector;
            _comparer = comparer;
            _descending = descending;
            _parent = parent;
        }

        public override AsyncIterator<TElement> Clone()
        {
            return new OrderedAsyncEnumerable<TElement, TKey>(source, _keySelector, _comparer, _descending, _parent);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_enumerator != null)
            {
                _enumerator.Dispose();
                _enumerator = null;
            }

            if (_parentEnumerator != null)
            {
                await _parentEnumerator.DisposeAsync().ConfigureAwait(false);
                _parentEnumerator = null;
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        protected override async ValueTask<bool> MoveNextCore(CancellationToken cancellationToken)
        {
            switch (state)
            {
                case AsyncIteratorState.Allocated:

                    await Initialize(cancellationToken).ConfigureAwait(false);

                    _enumerator = enumerable.GetEnumerator();
                    state = AsyncIteratorState.Iterating;
                    goto case AsyncIteratorState.Iterating;

                case AsyncIteratorState.Iterating:
                    if (_enumerator.MoveNext())
                    {
                        current = _enumerator.Current;
                        return true;
                    }

                    await DisposeAsync().ConfigureAwait(false);
                    break;
            }

            return false;
        }

        internal override async Task Initialize(CancellationToken cancellationToken)
        {
            if (_parent == null)
            {
                var buffer = await source.ToList(cancellationToken).ConfigureAwait(false);
                enumerable = (!_descending ? buffer.OrderBy(_keySelector, _comparer) : buffer.OrderByDescending(_keySelector, _comparer));
            }
            else
            {
                _parentEnumerator = _parent.GetAsyncEnumerator(cancellationToken);
                await _parent.Initialize(cancellationToken).ConfigureAwait(false);
                enumerable = _parent.enumerable.CreateOrderedEnumerable(_keySelector, _comparer, _descending);
            }
        }
    }

    internal sealed class OrderedAsyncEnumerableWithTask<TElement, TKey> : OrderedAsyncEnumerable<TElement>
    {
        private readonly IComparer<TKey> _comparer;
        private readonly bool _descending;
        private readonly Func<TElement, Task<TKey>> _keySelector;
        private readonly OrderedAsyncEnumerable<TElement> _parent;

        private IEnumerator<TElement> _enumerator;
        private IAsyncEnumerator<TElement> _parentEnumerator;

        public OrderedAsyncEnumerableWithTask(IAsyncEnumerable<TElement> source, Func<TElement, Task<TKey>> keySelector, IComparer<TKey> comparer, bool descending, OrderedAsyncEnumerable<TElement> parent)
        {
            Debug.Assert(source != null);
            Debug.Assert(keySelector != null);
            Debug.Assert(comparer != null);

            this.source = source;
            _keySelector = keySelector;
            _comparer = comparer;
            _descending = descending;
            _parent = parent;
        }

        public override AsyncIterator<TElement> Clone()
        {
            return new OrderedAsyncEnumerableWithTask<TElement, TKey>(source, _keySelector, _comparer, _descending, _parent);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_enumerator != null)
            {
                _enumerator.Dispose();
                _enumerator = null;
            }

            if (_parentEnumerator != null)
            {
                await _parentEnumerator.DisposeAsync().ConfigureAwait(false);
                _parentEnumerator = null;
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        protected override async ValueTask<bool> MoveNextCore(CancellationToken cancellationToken)
        {
            switch (state)
            {
                case AsyncIteratorState.Allocated:

                    await Initialize(cancellationToken).ConfigureAwait(false);

                    _enumerator = enumerable.GetEnumerator();
                    state = AsyncIteratorState.Iterating;
                    goto case AsyncIteratorState.Iterating;

                case AsyncIteratorState.Iterating:
                    if (_enumerator.MoveNext())
                    {
                        current = _enumerator.Current;
                        return true;
                    }

                    await DisposeAsync().ConfigureAwait(false);
                    break;
            }

            return false;
        }

        internal override async Task Initialize(CancellationToken cancellationToken)
        {
            if (_parent == null)
            {
                var buffer = await source.ToList(cancellationToken).ConfigureAwait(false);
                enumerable = (!_descending ? buffer.OrderByAsync(_keySelector, _comparer) : buffer.OrderByDescendingAsync(_keySelector, _comparer));
            }
            else
            {
                _parentEnumerator = _parent.GetAsyncEnumerator(cancellationToken);
                await _parent.Initialize(cancellationToken).ConfigureAwait(false);
                enumerable = _parent.enumerable.CreateOrderedEnumerableAsync(_keySelector, _comparer, _descending);
            }
        }
    }

    internal static class EnumerableSortingExtensions
    {
        // TODO: Implement async sorting.

        public static IOrderedEnumerable<TSource> OrderByAsync<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, Task<TKey>> keySelector, IComparer<TKey> comparer)
        {
            return source.OrderBy(key => keySelector(key).GetAwaiter().GetResult(), comparer);
        }

        public static IOrderedEnumerable<TSource> OrderByDescendingAsync<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, Task<TKey>> keySelector, IComparer<TKey> comparer)
        {
            return source.OrderByDescending(key => keySelector(key).GetAwaiter().GetResult(), comparer);
        }

        public static IOrderedEnumerable<TSource> CreateOrderedEnumerableAsync<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, Task<TKey>> keySelector, IComparer<TKey> comparer, bool descending)
        {
            return source.CreateOrderedEnumerable(key => keySelector(key).GetAwaiter().GetResult(), comparer, descending);
        }
    }
}
