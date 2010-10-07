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
using Aurigma.GraphicsMill.Codecs;
using System.Diagnostics;
using fbs.ImageResizer;
using fbs;
namespace PsdRenderer
{
    [AspNetHostingPermission(SecurityAction.Demand, Level = AspNetHostingPermissionLevel.Medium)]
    [AspNetHostingPermission(SecurityAction.InheritanceDemand, Level = AspNetHostingPermissionLevel.High)]
    public class PsdProvider : VirtualPathProvider
    {

        string _pathPrefix = "~/databaseimages/";
        string _connectionString = null;
        string _binaryQueryString = 
            "SELECT Content FROM Images WHERE ImageID=@id";
        string _modifiedDateQuery = 
            "Select ModifiedDate, CreatedDate From Images WHERE ImageID=@id";
        string _existsQuery = "Select COUNT(ImageID) From Images WHERE ImageID=@id";

        private System.Data.SqlDbType idType = System.Data.SqlDbType.Int;

        public PsdProvider()
            : base()
        {
            //Override connection string here
            _connectionString = ConfigurationManager.ConnectionStrings["database"].ConnectionString;
        }
        /// <summary>
        /// Returns a stream to the 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Stream getStream(string virtualPath)
        {

            Stopwatch sw = new Stopwatch();
            sw.Start();

            //Renderer object
            IPsdRenderer renderer = null;
            //The querystring-specified renderer name
            string sRenderer = null;
            if (HttpContext.Current.Request.QueryString["renderer"] != null) sRenderer = HttpContext.Current.Request.QueryString["renderer"].ToLowerInvariant();
            //Build the correct renderer
            if (("graphicsmill").Equals(sRenderer))
                renderer = new GraphicsMillRenderer();
            else
                renderer = new PsdPluginRenderer();
            
            //Bitmap we will render to
            System.Drawing.Bitmap b = null;

            //Which layers do we show?
            string showLayersWith = "12288";
            if (HttpContext.Current.Request.QueryString["showlayerswith"] != null) showLayersWith = HttpContext.Current.Request.QueryString["showlayerswith"];

            //Open the file.
            using (Stream s = System.IO.File.OpenRead(getPhysicalPath(virtualPath))){
                //Time just the parsing/rendering
                Stopwatch swRender = new Stopwatch();
                swRender.Start();

                //Use the selected renderer to parse the file and compose the layers, using this delegate callback to determine which layers to show.
                b = renderer.Render(s,
                    delegate(int index, string name, bool visibleNow)
                    {
                        if (visibleNow) return true;
                        return (index < 6 || name.Contains(showLayersWith));
                    });
                //How fast?
                swRender.Stop();
                HttpContext.Current.Trace.Write("Using encoder " + renderer.ToString() + ", rendering stream to a composed Bitmap instance took " + swRender.ElapsedMilliseconds.ToString() + "ms");

            }
            //Memory stream for encoding the file
            MemoryStream ms = new MemoryStream();
            //Encode image to memory stream, then seek the stream to byte 0
            using (b)
            {
                //Use whatever settings appear in the URL 
                ImageOutputSettings ios = new ImageOutputSettings(yrl.Current);
                ios.SaveImage(ms, b);
                ms.Seek(0, SeekOrigin.Begin); //Reset stream for reading
            }

            sw.Stop();
            HttpContext.Current.Trace.Write("Total time, including encoding: " + sw.ElapsedMilliseconds.ToString() + "ms");

            return ms;
        }

        public static IList<ITextLayer> getTextLayers(string virtualPath)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            //Renderer object
            IPsdRenderer renderer = null;
            //The querystring-specified renderer name
            string sRenderer = null;
            if (HttpContext.Current.Request.QueryString["renderer"] != null) sRenderer = HttpContext.Current.Request.QueryString["renderer"].ToLowerInvariant();
            //Build the correct renderer
            if (("graphicsmill").Equals(sRenderer))
                renderer = new GraphicsMillRenderer();
            else
                renderer = new PsdPluginRenderer();

            IList<ITextLayer> layers = null;


            //Open the file.
            using (Stream s = System.IO.File.OpenRead(getPhysicalPath(virtualPath)))
            {
                //Time just the parsing/rendering
                Stopwatch swRender = new Stopwatch();
                swRender.Start();

                //Use the selected renderer to parse the file and compose the layers, using this delegate callback to determine which layers to show.
                layers = renderer.GetTextLayers(s);
                //How fast?
                swRender.Stop();
                HttpContext.Current.Trace.Write("Using decoder " + renderer.ToString() + ", enumerating layers took " + swRender.ElapsedMilliseconds.ToString() + "ms");

            }
            IList<ITextLayer> filtered = new List<ITextLayer>();
            //Which layers do we show?
            string showLayersWith = "12288";
            if (HttpContext.Current.Request.QueryString["showlayerswith"] != null) showLayersWith = HttpContext.Current.Request.QueryString["showlayerswith"];

            for (int i = 0; i < layers.Count; i++){
                bool add = layers[i].Visible ;
                if (!add && (layers[i].Name.Contains(showLayersWith))) add = true;
                if (add) filtered.Add(layers[i]);
            }
            
            sw.Stop();
            HttpContext.Current.Trace.Write("Total time for enumerating, including file reading: " + sw.ElapsedMilliseconds.ToString() + "ms");
            return filtered;

        }

        /// <summary>
        /// Returns DateTime.MinValue if there are no rows, or no values on the row.
        /// Executes _modifiedDateQuery, then returns the first non-null datetime value on the first row.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DateTime getDateModifiedUtc(string virtualPath){
            return System.IO.File.GetLastWriteTimeUtc(getPhysicalPath(virtualPath));
        }

        public SqlConnection GetConnectionObj(){
            return new SqlConnection(_connectionString);
        }

        protected override void Initialize()
        {

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
            return (System.IO.Path.GetFileName(virtualPath).ToLowerInvariant().Contains(".psd."));
        }

        static string  getPhysicalPath(string virtualPath)
        {
            int ix = virtualPath.ToLowerInvariant().LastIndexOf(".psd");
            return HttpContext.Current.Request.MapPath(virtualPath.Substring(0, ix + 4));
        }


        public override bool FileExists(string virtualPath)
        {
            if (IsPathVirtual(virtualPath))
            {
                return System.IO.File.Exists(getPhysicalPath(virtualPath));
            }
            else
                return Previous.FileExists(virtualPath);
        }

        bool PSDExists(string virtualPath)
        {
            return IsPathVirtual(virtualPath) && System.IO.File.Exists(getPhysicalPath(virtualPath));
        }

        public override VirtualFile GetFile(string virtualPath)
        {
            if (PSDExists(virtualPath))
                return new PsdVirtualFile(virtualPath, this);
            else
                return Previous.GetFile(virtualPath);
        }

        public override CacheDependency GetCacheDependency(
          string virtualPath,
          System.Collections.IEnumerable virtualPathDependencies,
          DateTime utcStart)
        {
            //Maybe the database is also involved? 
            return Previous.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
        }
    }


    [AspNetHostingPermission(SecurityAction.Demand, Level = AspNetHostingPermissionLevel.Minimal)]
    [AspNetHostingPermission(SecurityAction.InheritanceDemand, Level = AspNetHostingPermissionLevel.Minimal)]
    public class PsdVirtualFile : VirtualFile, fbs.ImageResizer.IVirtualFileWithModifiedDate
    {
  
        private PsdProvider provider;

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

        public PsdVirtualFile(string virtualPath, PsdProvider provider)
            : base(virtualPath)
        {
            this.provider = provider;
        }

        /// <summary>
        /// Returns a stream to the database blob associated with the id
        /// </summary>
        /// <returns></returns>
        public override Stream Open(){ return provider.getStream(this.VirtualPath);}

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