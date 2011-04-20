﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Configuration;

namespace ImageResizer.Plugins.DiskCache.Cleanup {
    public class CleanupQueue {
        LinkedList<CleanupWorkItem> queue = null;
        public CleanupQueue() {
            queue = new LinkedList<CleanupWorkItem>();
        }

        protected readonly object _sync = new object();

        public void Queue(CleanupWorkItem item) {
            lock (_sync) {
                queue.AddLast(item);
            }
        }
        public void Insert(CleanupWorkItem item) {
            lock (_sync) {
                queue.AddFirst(item);
            }
        }
        public void QueueRange(IEnumerable<CleanupWorkItem> items) {
            lock (_sync) {
                foreach(CleanupWorkItem item in items)  
                    queue.AddLast(item);
            }
        }
        /// <summary>
        /// Inserts the specified list of items and the end of the queue. They will be next items popped.
        /// They will pop off the list in the same order they exist in 'items' (i.e, they are inserted in reverse order).
        /// </summary>
        /// <param name="items"></param>
        public void InsertRange(IList<CleanupWorkItem> items) {
            lock (_sync) {
                ReverseEnumerable<CleanupWorkItem> reversed = new ReverseEnumerable<CleanupWorkItem>(new System.Collections.ObjectModel.ReadOnlyCollection<CleanupWorkItem>(items));
                foreach (CleanupWorkItem item in reversed)
                    queue.AddFirst(item);
            }
        }
        public CleanupWorkItem Pop() {
            lock (_sync) {
                CleanupWorkItem i = queue.Count > 0 ? queue.First.Value : null;
                if (i != null) queue.RemoveFirst();
                return i;
            }
        }

        public bool IsEmpty {
            get {
                lock (_sync) return queue.Count > 0;
            }
        }

        public void Clear() {
            lock (_sync) {
                queue.Clear();
            }
        }
        /// <summary>
        /// Performs an atomic clear and enqueue of the specified item
        /// </summary>
        /// <param name="item"></param>
        public void ReplaceWith(CleanupWorkItem item) {
            lock (_sync) {
                queue.Clear();
                queue.AddLast(item);
            }
        }
    }
}
