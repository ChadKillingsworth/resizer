﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ImageResizer.Plugins.DiskCache.Cleanup;
using ImageResizer.Configuration.Issues;

namespace ImageResizer.Plugins.DiskCache {

   
    public class CleanupManager:IIssueProvider, IDisposable {
        protected CustomDiskCache cache = null;
        protected CleanupStrategy cs = null;
        protected CleanupQueue queue = null;
        protected CleanupWorker worker = null;
        

        public CleanupManager(CustomDiskCache cache, CleanupStrategy cs) {
            this.cache = cache;
            this.cs = cs;
            queue = new CleanupQueue();
            //Called each request
            cache.CacheResultReturned += delegate(CustomDiskCache sender, CacheResult r) {
                if (r.Result == CacheQueryResult.Miss)
                    this.AddedFile(r.RelativePath); //It was either updated or added.
                else
                    this.BeLazy();
            };
            //Called when the filesystem changes unexpectedly.
            cache.Index.FileDisappeared += delegate(string relativePath, string physicalPath) {
                //Stop everything ASAP and start a brand new cleaning run.
                queue.ReplaceWith(new CleanupWorkItem(CleanupWorkItem.Kind.CleanFolderRecursive, "", cache.PhysicalCachePath));
                worker.MayHaveWork();
            };

            worker = new CleanupWorker(cs,queue,cache);
        }
        

        

        /// <summary>
        /// Notifies the CleanupManager that a request is in process. Helps CleanupManager optimize background work so it doesn't interfere with request processing.
        /// </summary>
        public void BeLazy() {
            worker.BeLazy();
        }
        /// <summary>
        /// Notifies the CleanupManager that a file was added under the specified relative path. Allows CleanupManager to detect when a folder needs cleanup work.
        /// </summary>
        /// <param name="relativePath"></param>
        public void AddedFile(string relativePath) {

            //TODO: Maybe we shouldn't queue a task to compare the numbers every time a file is added? 

            int slash = relativePath.LastIndexOf('/');
            string folder = slash > -1 ? relativePath.Substring(0, slash) : "";
            char c = System.IO.Path.DirectorySeparatorChar;
            string physicalFolder =  cache.PhysicalCachePath.TrimEnd(c) + c + folder.Replace('/',c).Replace('\\',c).Trim(c);

            //Only queue the item if it doesn't already exist.
            if (queue.QueueIfUnique(new CleanupWorkItem(CleanupWorkItem.Kind.CleanFolderRecursive, folder,physicalFolder)))
                worker.MayHaveWork();
        }

        public void CleanAll() {
            //Only queue the item if it doesn't already exist.
            if (queue.QueueIfUnique(new CleanupWorkItem(CleanupWorkItem.Kind.CleanFolderRecursive, "", cache.PhysicalCachePath)))
                worker.MayHaveWork();
        }

        public void UsedFile(string relativePath, string physicalPath) {
            //Bump the date in memory
            cache.Index.bumpDateIfExists(relativePath);
            //Make sure the 'flush' job for the file is in the queue somewhere, so the access date will get written to disk.
            queue.QueueIfUnique(new CleanupWorkItem(CleanupWorkItem.Kind.FlushAccessedDate, relativePath, physicalPath));
            //In case it's paused
            worker.MayHaveWork();
        }


        public void Dispose() {
            worker.Dispose();
        }

        public IEnumerable<IIssue> GetIssues() {
            if (worker != null) return worker.GetIssues();
            return new IIssue[] { };
        }


    }
   
}
