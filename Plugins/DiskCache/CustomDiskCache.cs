﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Caching;
using ImageResizer.Util;
using System.IO;
using ImageResizer.Configuration.Logging;
using System.Diagnostics;
using System.Threading;

namespace ImageResizer.Plugins.DiskCache {
    public delegate void CacheResultHandler(CustomDiskCache sender, CacheResult r);

    /// <summary>
    /// Handles access to a disk-based file cache. Handles locking and versioning. 
    /// Supports subfolders for scalability.
    /// </summary>
    public class CustomDiskCache {
        protected string physicalCachePath;

        public string PhysicalCachePath {
            get { return physicalCachePath; }
        }
        protected int subfolders;
        protected bool hashModifiedDate;
        protected ILoggerProvider lp;
        public CustomDiskCache(ILoggerProvider lp, string physicalCachePath, int subfolders, bool hashModifiedDate) {
            this.lp = lp;
            this.physicalCachePath = physicalCachePath;
            this.subfolders = subfolders;
            this.hashModifiedDate = hashModifiedDate;
        }
        /// <summary>
        /// Fired immediately before GetCachedFile return the result value. 
        /// </summary>
        public event CacheResultHandler CacheResultReturned; 


        protected LockProvider locks = new LockProvider();
        /// <summary>
        /// Provides string-based locking for file write access.
        /// </summary>
        public LockProvider Locks {
            get { return locks; }
        }


        private CacheIndex index = new CacheIndex();
        /// <summary>
        /// Provides an in-memory index of the cache.
        /// </summary>
        public CacheIndex Index {
            get { return index; }
        }
        
        


        public CacheResult GetCachedFile(string keyBasis, string extension, ResizeImageDelegate writeCallback, DateTime sourceModifiedUtc, int timeoutMs) {
            Stopwatch sw = null;
            if (lp.Logger != null) { sw = new Stopwatch(); sw.Start(); }


            bool hasModifiedDate = !sourceModifiedUtc.Equals(DateTime.MinValue);


            //Hash the modified date if needed.
            if (hashModifiedDate && hasModifiedDate)
                keyBasis += "|" + sourceModifiedUtc.Ticks.ToString();

            //Relative to the cache directory. Not relative to the app or domain root
            string relativePath = new UrlHasher().hash(keyBasis, subfolders, "/") + '.' + extension;

            //Physical path
            string physicalPath = PhysicalCachePath.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar +
                    relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar);


            CacheResult result = new CacheResult(CacheQueryResult.Hit, physicalPath,relativePath);
            
            //On the first check, verify the file exists using System.IO directly (the last 'true' parameter)
            if (((!hasModifiedDate || hashModifiedDate) && !Index.existsCertain(relativePath, physicalPath)) || !Index.modifiedDateMatchesCertainExists(sourceModifiedUtc, relativePath, physicalPath)) {
                
                //Lock execution using relativePath as the sync basis. Ignore casing differences.
                if (!Locks.TryExecute(relativePath.ToUpperInvariant(), timeoutMs,
                    delegate() {

                        //On the second check, use cached data for speed. The cached data should be updated if another thread updated a file (but not if another process did).
                        if (((!hasModifiedDate || hashModifiedDate) && !Index.exists(relativePath, physicalPath)) || !Index.modifiedDateMatches(sourceModifiedUtc, relativePath, physicalPath)) {

                            //Create subdirectory if needed.
                            if (!Directory.Exists(Path.GetDirectoryName(physicalPath))) {
                                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath));
                                if (lp.Logger != null) lp.Logger.Debug("Creating missing parent directory {0}",Path.GetDirectoryName(physicalPath));
                            }

                            //Open stream 
                            //Catch IOException, and if it is a file lock,
                            // - (and hashmodified is true), then it's another process writing to the file, and we can serve the file afterwards
                            // - (and hashmodified is false), then it could either be an IIS read lock or another process writing to the file. Correct behavior is to kill the request here, as we can't guarantee accurate image data.
                            // I.e, hashmodified=true is the only supported setting for multi-process environments.
                            //TODO: Catch UnathorizedAccessException and log issue about file permissions.
                            //... If we can wait for a read handle for a specified timeout.
                            try {
                                System.IO.FileStream fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
                                
                                using (fs) {
                                    //Run callback to write the cached data
                                    writeCallback(fs);
                                }

                                DateTime createdUtc = DateTime.UtcNow;
                                //Update the write time to match - this is how we know whether they are in sync.
                                if (hasModifiedDate) System.IO.File.SetLastWriteTimeUtc(physicalPath, sourceModifiedUtc);
                                //Set the created date, so we know the last time we updated the cache.s
                                System.IO.File.SetCreationTimeUtc(physicalPath, createdUtc);
                                //Update index
                                Index.setCachedFileInfo(relativePath, new CachedFileInfo(sourceModifiedUtc, createdUtc, createdUtc));
                                //This was a cache miss
                                result.Result = CacheQueryResult.Miss;
                            } catch (IOException ex) {
                                
                                if (hashModifiedDate && hasModifiedDate && IsFileLocked(ex)) {
                                    //Somehow in between verifying the file didn't exist and trying to create it, the file was created and locked by someone else.
                                    //When hashModifiedDate==true, we don't care what the file contains, we just want it to exist. If the file is available for 
                                    //reading within timeoutMs, let's recursively call this method so that we can consider it a hit. 
                                    Stopwatch waitForFile = new Stopwatch();
                                    bool opened = false;
                                    while (!opened && waitForFile.ElapsedMilliseconds < timeoutMs)
                                    {
                                        waitForFile.Start();
                                        try{
                                            using (FileStream temp = new FileStream(physicalPath, FileMode.Open,  FileAccess.Read, FileShare.ReadWrite))
                                                opened = true;
                                        } catch(IOException iex){
                                            if (IsFileLocked(iex))
                                                Thread.Sleep((int)Math.Min(30, Math.Round((float)timeoutMs / 3.0))); 
                                            else throw iex;
                                        }
                                        waitForFile.Stop();
                                    }
                                    if (!opened) throw; //By not throwing an exception, it is considered a hit by the rest of the code.

                                }else throw;
                            }

                        }
                    })) {
                    //On failure
                    result.Result = CacheQueryResult.Failed;
                }
                
            }
            if (lp.Logger != null) {
                sw.Stop();
                lp.Logger.Trace("({0}ms): {1} for {2}", sw.ElapsedMilliseconds, result.Result.ToString(), result.RelativePath); 
            }
            //Fire event
            if (CacheResultReturned != null) CacheResultReturned(this, result);
            return result;
        }


        private static bool IsFileLocked(IOException exception) {
            int errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(exception) & ((1 << 16) - 1);
            return errorCode == 32 || errorCode == 33;
        }


    }
}
