﻿using System;
using System.Threading;

namespace Fabric.Terminology.Domain.Threading
{
    internal class SlimWriterLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;

        public SlimWriterLock(ReaderWriterLockSlim readWriteLock)
        {
            _lock = readWriteLock;
            _lock.EnterReadLock();
        }

        void IDisposable.Dispose()
        {
            _lock.ExitWriteLock();
        }
    }
}