﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ionic.Zip;
using System.IO;

namespace ImageResizer.ReleaseBuilder {
    public class Package:IDisposable {

        ZipFile z = null;
        string basePath;
        /// <summary>
        /// Creates a zip file a the specified location
        /// </summary>
        /// <param name="zipFile"></param>
        /// <param name="basePath"></param>
        public Package(string zipFile, string basePath) {
            if (File.Exists(zipFile)) {
                return;
            }
            z = new ZipFile(zipFile, Console.Out);
            z.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;


            this.basePath = basePath.TrimEnd('/','\\');
        }

        public void Add(IEnumerable<string> files, string targetDir = null) {
            if (z == null) return;
            string lastDir = null;
            foreach (string s in files) {
                string dir = targetDir;
                if (dir == null){
                    string relPath = Path.GetDirectoryName(s);
                    if (relPath.StartsWith(basePath)) dir = relPath.Substring(basePath.Length).TrimStart('\\','/');
                    else throw new Exception("Path outside baseDir: " + relPath + " , " + basePath);
                }
                dir = dir ?? "";
                if (dir != lastDir) Console.WriteLine("\nIn folder \"" + dir + "\":");
                z.AddFile(s, dir);
                lastDir = dir;

            }
        }
        

        public void Dispose() {
            if (z == null) {
                Console.WriteLine("No changes were made to the package");
                return;
            }
            z.Save();

            Console.WriteLine("Archive created successfully: " + z.Name);
            var bigFiles = (from e in z.Entries
                            where e.CompressedSize > 100000
                            select e).OrderByDescending<ZipEntry, long>(delegate(ZipEntry e) { return e.CompressedSize; });

            foreach (ZipEntry entry in bigFiles) {
          
                Console.WriteLine((entry.CompressedSize / 1024) + "k " + entry.FileName);
            }

            
            z.Dispose();
        }
    }
}
