﻿using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Hosting;
using System.Security.Permissions;
using System.Web.Caching;
using System.IO;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Configuration;
using System.Diagnostics;
using ImageResizer;
using ImageResizer.Util;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using PhotoshopFile;
using PhotoshopFile.Text;
using ImageResizer.Plugins;
using ImageResizer.Configuration;
using ImageResizer.Encoding;
namespace ImageResizer.Plugins.PsdReader
{
    [AspNetHostingPermission(SecurityAction.Demand, Level = AspNetHostingPermissionLevel.Medium)]
    [AspNetHostingPermission(SecurityAction.InheritanceDemand, Level = AspNetHostingPermissionLevel.High)]
    public class PsdReaderPlugin : VirtualPathProvider, IPlugin
    {
        /// <summary>
        /// Registers the PsdReader plugin as a virtual path provider.
        /// </summary>
        /// <returns></returns>
        public IPlugin Install(Config c) {
            HostingEnvironment.RegisterVirtualPathProvider(this);
            c.Plugins.add_plugin(this);
            return this;
        }
        public bool Uninstall(Config c) {
            c.Plugins.remove_plugin(this);
            return false;//Cannot truly remove a VPP
        }



        public PsdReaderPlugin() : base()
        {
        }
      
        /// <summary>
        /// Allows reading from the querystring, using context.Items["modifiedQueryString"] and falling back to Request.Querystring if it is missing.
        /// This behavior allows the reader to read values that have been changed by the rewrite events
        /// </summary>
        protected NameValueCollection queryString
        {
            get
            {
                if (HttpContext.Current.Items["modifiedQueryString"] != null)
                    return new NameValueCollection((NameValueCollection)HttpContext.Current.Items["modifiedQueryString"]);
                else
                    return new NameValueCollection(HttpContext.Current.Request.QueryString);
            }
        }


        /// <summary>
        /// Returns an re-encoded stream of the PSD, using whatever extension was appeneded after .psd, or, if &format was specified, that encoding
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Stream getStream(string virtualPath)
        {

            Stopwatch sw = new Stopwatch();
            sw.Start();

            System.Drawing.Bitmap b = getBitmap(virtualPath);
            if (b.Tag == null) b.Tag = virtualPath;
            //Memory stream for encoding the file
            MemoryStream ms = new MemoryStream();
            //Encode image to memory stream, then seek the stream to byte 0
            using (b)
            {
                //Use whatever settings appear in the URL. TODO: may need to fix this, doesn't use path right.
                IEncoder ios = Config.Current.Plugins.EncoderProvider.GetEncoder(new ResizeSettings(queryString),b);
                ios.Write(b, ms);
                ms.Seek(0, SeekOrigin.Begin); //Reset stream for reading
            }

            sw.Stop();
            trace("Total time, including encoding: " + sw.ElapsedMilliseconds.ToString() + "ms");

            return ms;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public System.Drawing.Bitmap getBitmap(string virtualPath)
        {

            //Bitmap we will render to
            System.Drawing.Bitmap b = null;
            VirtualFile vf = Previous.GetFile(stripFakeExtension(virtualPath));
            
            //MemCachedFile file = MemCachedFile.GetCachedFile(getPhysicalPath(virtualPath));
            using (Stream s = vf.Open())
            {
                
                //Time just the parsing/rendering
                Stopwatch swRender = new Stopwatch();
                swRender.Start();

                PsdFile psdFile = new PsdFile();
                psdFile.Load(s);
                //Load background layer
                b = ImageDecoder.DecodeImage(psdFile); //Layers collection doesn't include the composed layer

                //How fast?
                swRender.Stop();
                trace("Parsing and rendering PSD to a Bitmap instance took " + swRender.ElapsedMilliseconds.ToString() + "ms");
            }
            return b;
        }

 

        private static void trace(string msg)
        {
            if (HttpContext.Current == null)
                System.Diagnostics.Debug.Write(msg);
            else
                HttpContext.Current.Trace.Write(msg);
        }


        
        public DateTime getDateModifiedUtc(string virtualPath){
            IVirtualFileWithModifiedDate prev = Previous.GetFile(stripFakeExtension(virtualPath)) as IVirtualFileWithModifiedDate;
            string physicalPath = getPhysicalPath(virtualPath);
            if (prev != null)
                return prev.ModifiedDateUTC;
            else if (System.IO.File.Exists(physicalPath))
                return System.IO.File.GetLastWriteTimeUtc(physicalPath);
            else return DateTime.MinValue;
        }


        /// <summary>
        ///   Determines whether a specified virtual path is within
        ///   the virtual file system.
        /// </summary>
        /// <param name="virtualPath">An absolute virtual path.</param>
        /// <returns>
        ///   true if the virtual path is within the 
        ///   virtual file sytem; otherwise, false.
        /// </returns>
        bool IsPathVirtual(string virtualPath)
        {
            return System.IO.Path.GetFileName(virtualPath).LastIndexOf(".psd.", StringComparison.OrdinalIgnoreCase) > -1;
        }
        /// <summary>
        /// Strips everything after .psd off.
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <returns></returns>
        static string stripFakeExtension(string virtualPath) {
            int ix = virtualPath.ToLowerInvariant().LastIndexOf(".psd", StringComparison.OrdinalIgnoreCase);
            if (ix < 0) return virtualPath;
            return virtualPath.Substring(0, ix + 4);
        }
        static string  getPhysicalPath(string virtualPath)
        {
            string str = stripFakeExtension(virtualPath);
            if (HttpContext.Current != null) return HttpContext.Current.Request.MapPath(str);
            else return str.TrimStart('~','/').Replace('/','\\');
        }


        public override bool FileExists(string virtualPath)
        {
            if (IsPathVirtual(virtualPath))
                return Previous.FileExists(stripFakeExtension(virtualPath));
            else
                return Previous.FileExists(virtualPath);
        }

        bool PSDExists(string virtualPath)
        {
            return IsPathVirtual(virtualPath) && Previous.FileExists(stripFakeExtension(virtualPath));
        }

        public override VirtualFile GetFile(string virtualPath)
        {
            if (PSDExists(virtualPath))
                return new PsdReaderVirtualFile(virtualPath, this);
            else
                return Previous.GetFile(virtualPath);
        }

        public override CacheDependency GetCacheDependency(
          string virtualPath,
          System.Collections.IEnumerable virtualPathDependencies,
          DateTime utcStart)
        {
            //Maybe the database is also involved? 
            return Previous.GetCacheDependency(stripFakeExtension(virtualPath), virtualPathDependencies, utcStart);
        }
    }


    [AspNetHostingPermission(SecurityAction.Demand, Level = AspNetHostingPermissionLevel.Minimal)]
    [AspNetHostingPermission(SecurityAction.InheritanceDemand, Level = AspNetHostingPermissionLevel.Minimal)]
    public class PsdReaderVirtualFile : VirtualFile, IVirtualFileWithModifiedDate, IVirtualBitmapFile
    {
  
        private PsdReaderPlugin provider;

        private Nullable<bool> _exists = null;
        private Nullable<DateTime> _fileModifiedDate = null;
   

        /// <summary>
        /// Returns true if the row exists. 
        /// </summary>
        public bool Exists
        {
            get {
                if (_exists == null) _exists = provider.FileExists(this.VirtualPath);
                return _exists.Value;
            }
        }

        public PsdReaderVirtualFile(string virtualPath, PsdReaderPlugin provider)
            : base(virtualPath)
        {
            this.provider = provider;
        }

        /// <summary>
        /// Returns a stream of the encoded file bitmap using the current request querystring.
        /// </summary>
        /// <returns></returns>
        public override Stream Open(){ return provider.getStream(this.VirtualPath);}
        /// <summary>
        /// Returns a composed bitmap of the file using request querystring paramaters.
        /// </summary>
        /// <returns></returns>
        public System.Drawing.Bitmap GetBitmap() { return provider.getBitmap(this.VirtualPath); }

        /// <summary>
        /// Returns the last modified date of the row. Cached for performance.
        /// </summary>
        public DateTime ModifiedDateUTC{
            get{
                if (_fileModifiedDate == null) _fileModifiedDate = provider.getDateModifiedUtc(this.VirtualPath);
                return _fileModifiedDate.Value;
            }
        }
      
    }
}