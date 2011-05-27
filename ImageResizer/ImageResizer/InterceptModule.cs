/**
 * Written by Nathanael Jones 
 * http://nathanaeljones.com
 * nathanael.jones@gmail.com
 * 
 * This is a discontinued version of the Image Resizer, and is being replaced by Version 3.
 * Visit http://imageresizing.net/ to download version 3.
 * Complete migration docs are available at: http://imageresizing.net/docs/2to3/
 * 
 * Although I typically release my components for free, I decided to charge a 
 * 'download fee' for this one to help support my other open-source projects. 
 * Don't worry, this component is still open-source, and the license permits 
 * source redistribution as part of a larger system. However, I'm asking that 
 * people who want to integrate this component purchase the download instead 
 * of ripping it out of another open-source project. My free to non-free LOC 
 * (lines of code) ratio is still over 40 to 1, and I plan on keeping it that 
 * way. I trust this will keep everybody happy.
 * 
 * By purchasing the download, you are permitted to 
 * 
 * 1) Modify and use the component in all of your projects. 
 * 
 * 2) Redistribute the source code as part of another project, provided 
 * the component is less than 5% of the project (in lines of code), 
 * and you keep this information attached.
 * 
 * 3) If you received the source code as part of another open source project, 
 * you cannot extract it (by itself) for use in another project without purchasing a download 
 * from http://nathanaeljones.com/. If nathanaeljones.com is no longer running, and a download
 * cannot be purchased, then you may extract the code.
 * 
 * Disclaimer of warranty and limitation of liability continued at http://nathanaeljones.com/11151_Image_Resizer_License
 **/

//Clear the image cache before upgrading.. .not sure why it's needed, but images appear corrupted...

using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Drawing;
using fbs;
using System.IO;
using System.Web.Hosting;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Security.Principal;


namespace fbs.ImageResizer
{
    public class UrlEventArgs : EventArgs
    {
        protected string _virtualPath;
        protected NameValueCollection _queryString;

        public UrlEventArgs(string virtualPath, NameValueCollection queryString)
        {
            this._virtualPath = virtualPath;
            this._queryString = queryString;
        }

        public NameValueCollection QueryString
        {
            get { return _queryString; }
            set { _queryString = value; }
        }
        public string VirtualPath
        {
            get { return _virtualPath; }
            set { _virtualPath = value; }
        }
    }

    public delegate void UrlRewritingHook(InterceptModule sender, UrlEventArgs e);


    /// <summary>
    /// Monitors incoming image requests. Image requests that request resizing are processed. The resized images are immediately written to disk, and 
    /// the request is rewritten to the disk-cached resized version. This way IIS can handle the actual serving of the file.
    /// The disk-cache directory is protected through URL authorization.
    /// See CustomFolders.cs for any type of URL rewriting 
    /// </summary>
    public class InterceptModule:System.Web.IHttpModule
    {
        
        /// <summary>
        /// Called when the app is initialized
        /// </summary>
        /// <param name="context"></param>
        void System.Web.IHttpModule.Init(System.Web.HttpApplication context)
        {
            //We wait until after URL auth happens for security. (although we authorize again since we are doing URL rewriting)
            context.PostAuthorizeRequest += new EventHandler(CheckRequest_PostAuthorizeRequest);
            //This is where we set content-type and caching headers. content-type headers don't match the 
            //file extension when format= or thumbnail= is used, so we have to override them
            context.PreSendRequestHeaders += new EventHandler(context_PreSendRequestHeaders);
        }
        void System.Web.IHttpModule.Dispose() { }
        /// <summary>
        /// Fired during PostAuthorizeRequest, after ResizeExtension has been removed.
        /// Fired before CustomFolders and other URL rewriting events.
        /// <para>Only fired on accepted image types: ImageOutputSettings.IsAcceptedImageType(). 
        /// You can add acceptable image extentions using ImageOutputSettings, or you can add an 
        /// extra extension in the URL and remove it here. Example: .psd.jpg</para>
        /// </summary>
        public static event UrlRewritingHook Rewrite;
        /// <summary>
        /// Fired during PostAuthorizeRequest, after Rewrite.
        /// Any changes made here (which conflict) will be overwritten by the the current querystring values. I.e, this is a good place to specify default settings.
        /// <para>Only fired on accepted image types: ImageOutputSettings.IsAcceptedImageType()</para>
        /// </summary>
        public static event UrlRewritingHook RewriteDefaults;
        /// <summary>
        /// Fired after all other rewrite events and CustomFolders.cs 
        /// <para>Only fired on accepted image types: ImageOutputSettings.IsAcceptedImageType()</para>
        /// </summary>
        public static event UrlRewritingHook PostRewrite;


        /// <summary>
        /// Manages URL rewriting hooks
        /// </summary>
        /// <param name="e"></param>
        protected void processPath(UrlEventArgs e)
        {
            //Fire first event (results will stay in e)
            if (Rewrite != null) Rewrite( this,e);
            
            //Copy querystring for use in 'defaults' even
            NameValueCollection copy = new NameValueCollection(e.QueryString); //Copy so we can later overwrite q with the original values.
            
            //Fire defaults event.
            if (RewriteDefaults != null) RewriteDefaults(this,e);

            //Overwrite with querystring values again - this is what makes applyDefaults applyDefaults, vs. being applyOverrides.
            foreach (string k in copy)
                e.QueryString[k] = copy[k];

            
            //Call CustomFolders.cs to do resize(w,h,f)/ parsing and any other custom syntax.
            //The real virtual path should be returned (with the resize() stuff removed)
            //And q should be populated with the querystring values
            e.VirtualPath = CustomFolders.processPath(e.VirtualPath, e.QueryString);

            //Fire final event
            if (PostRewrite != null) PostRewrite(this,e);
        }

        /// <summary>
        /// This is where we filter requests and intercet those that want resizing performed.
        /// We first check for image extensions... 
        /// If it is one, then we run it through the CustomFolders methods to see if if there is custom resizing for it..
        /// If there still aren't any querystring params after that, then we ignore the request.
        /// If the file doesn't exist, we also ignore the request. They're going to cause a 404 anyway.
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void CheckRequest_PostAuthorizeRequest(object sender, EventArgs e)
        {
            //Get the http context, and only intercept requests where the Request object is actually populated
            HttpApplication app = sender as HttpApplication;
            if (app != null && app.Context != null && app.Context.Request != null)
            {
                string filePath = app.Context.Request.FilePath;

                //Allows users to append .ashx to all their image URLs instead of doing wildcard mapping.
                string altExtension = ConfigurationManager.AppSettings["ResizeExtension"];
                if (!string.IsNullOrEmpty(altExtension) && filePath.EndsWith(altExtension, StringComparison.OrdinalIgnoreCase))
                {
                    //Remove extension from filePath, since otherwise IsAcceptedImageType() will fail.
                    filePath = filePath.Substring(0, filePath.Length - altExtension.Length).TrimEnd('.');
                }

                //Is this an image request? Checks the file extension for .jpg, .png, .tiff, etc.
                if (ImageOutputSettings.IsAcceptedImageType(filePath))
                {
                    //Init the caching settings. These only take effect if the image is actually resized
                    //CustomFolders.cs can override these during processPath
                    app.Context.Items["ContentExpires"] = DateTime.Now.AddHours(24); //Default to 24 hours
                    string cacheSetting = ConfigurationManager.AppSettings["ImageResizerClientCacheMinutes"];
                    if (!string.IsNullOrEmpty(cacheSetting)){
                        double f;
                        if (double.TryParse(cacheSetting,out f)){
                            if (f >= 0)
                                app.Context.Items["ContentExpires"] = DateTime.Now.AddMinutes(f);
                            else
                                app.Context.Items["ContentExpires"] = null;
                        }
                    }
                    
                    //Copy the querystring
                    NameValueCollection q = new NameValueCollection(app.Context.Request.QueryString);

                    //Call events and CustomFolders.cs to do resize(w,h,f)/ parsing and any other custom syntax.
                    UrlEventArgs ue = new UrlEventArgs(filePath + app.Context.Request.PathInfo, q); //Create event object

                    //Modify event object
                    processPath(ue); 
                    
                    //Pull data back out of event object
                    string basePath = ue.VirtualPath;
                    q = ue.QueryString;

                    //dec-11-09  discontinuing AllowURLRewriting and AllowFolderResizeNotation
                    //If the path has changed, this will circumvent the URL auth system.
                    //Make sure the user has explicity allowed it through web.config
                   if (!basePath.Equals(app.Context.Request.Path))
                    {
                        //Make sure the resize() notation is allowed.
                        string allow = ConfigurationManager.AppSettings["AllowURLRewriting"];
                        if (string.IsNullOrEmpty(allow)) allow = ConfigurationManager.AppSettings["AllowFolderResizeNotation"];
                        if (string.IsNullOrEmpty(allow) || allow.Equals("false", StringComparison.OrdinalIgnoreCase)){
                            return; //Skip the request
                        }
                    }
                   
                    //See if resizing is wanted (i.e. one of the querystring commands is present).
                    //Called after processPath so processPath can add them if needed.
                    //Checks for thumnail, format, width, height, maxwidth, maxheight and a lot more
                    if (ImageManager.getBestInstance().HasResizingDirective(q))
                    {
                        IPrincipal user = app.Context.User as IPrincipal;

                        if (!"true".Equals(ConfigurationManager.AppSettings["DisableImageURLAuthorization"], StringComparison.OrdinalIgnoreCase)) //Fixed Oct. 18, 2010, - setting was inverted, missing !
                        {

                            // no user (must be anonymous...).. Or authentication doesn't work for this suffix. 
                            if (user == null)
                                user = new GenericPrincipal(new GenericIdentity(string.Empty, string.Empty), new string[0]);

                            if (!UrlAuthorizationModule.CheckUrlAccessForPrincipal(basePath, user, "GET"))
                            {
                                throw new HttpException(403, "Access denied.");
                            }

                        }

                        //Even if DisableImageURLAuthorization = true, we still want to prevent access to the /imagecache/ directory
                        if (new yrl(basePath).Local.StartsWith(DiskCache.GetCacheDir(), StringComparison.OrdinalIgnoreCase))
                        {
                            throw new HttpException(403, "Access denied to image cache folder.");
                        }

                        //Build a URL using the new basePath and the new Querystring q
                        yrl current = new yrl(basePath);
                        current.QueryString = q;

                        //Override normal behavior with virtualpathprovider
                        Boolean virtualFile = "true".Equals(ConfigurationManager.AppSettings["ImageResizerUseVirtualPathProvider"], StringComparison.OrdinalIgnoreCase);

                        //Fall back mode - only use virtual path provider as a fallback if the file doesn't exist - recommended setting.
                        Boolean virtualFileFallBack = "true".Equals(ConfigurationManager.AppSettings["ImageResizerUseVirtualPathProviderAsFallback"], StringComparison.OrdinalIgnoreCase);

                        //Store modified querystring in request for use by VirtualPathProviders
                        app.Context.Items["modifiedQueryString"] = q;

                        //If the file exists, resize it
                        if (current.FileExists)
                            ResizeRequest(app.Context, current);
                            //Fall back to the virtual path provider in case it is using a different data store
                        else if ((virtualFile || virtualFileFallBack) && HostingEnvironment.VirtualPathProvider.FileExists(getVPPSafePath(current)))
                            ResizeRequest(app.Context, current);

                        
                        
                    }
                }
            }
        }


        protected String getVPPSafePath(yrl y)
        {
            return yrl.GetAppFolderName().TrimEnd(new char[] { '/' }) + "/" + y.BaseFile;
        }


        /// <summary>
        /// Builds the physical path for the cached version, using the hashcode of the normalized URL.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public string getCachedVersionFilename(yrl request)
        {
            string dir = DiskCache.GetCacheDir();
            if (dir == null) return null;
            //Build the physical path of the cached version, using the hashcode of the normalized URL.

            SHA256 h = System.Security.Cryptography.SHA256.Create();
            byte[] hash = h.ComputeHash(new System.Text.UTF8Encoding().GetBytes(request.ToString().ToLower()));

            //If configured, place files in subfolders.
            string subfolder = getSubfolder(hash);
            subfolder = subfolder != null ? subfolder + System.IO.Path.DirectorySeparatorChar: "";

            //Can't use base64 hash... filesystem has case-insensitive lookup.
            //Would use base32, but too much code to bloat the resizer. Simple base16 encoding is fine
            return dir.TrimEnd('/', '\\') + System.IO.Path.DirectorySeparatorChar  + subfolder + Base16Encode(hash) + "." + new ImageOutputSettings(request).GetFinalExtension();
        }
        /// <summary>
        /// Returns a string for the subfolder name. The bits used are from the end of the hash - this should make
        /// the hashes in each directory more unique, and speed up performance (8.3 filename calculations are slow when lots of files share the same first 6 chars.
        /// Returns null if not configured. Rounds ImageCacheSubfolders up to the nearest power of two.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        protected string getSubfolder(byte[] hash)
        {
            if (string.IsNullOrEmpty(ConfigurationManager.AppSettings["ImageCacheSubfolders"])) return null;
            int subfolders = 0;
            if (int.TryParse(ConfigurationManager.AppSettings["ImageCacheSubfolders"],out subfolders))
            {
                int bits = (int)Math.Ceiling(Math.Log(subfolders, 2)); //Log2 to find the number of bits. round up.
                Debug.Assert(bits > 0);
                Debug.Assert(bits <= hash.Length * 8);

                byte[] subfolder = new byte[(int)Math.Ceiling((double)bits / 8.0)]; //Round up to bytes.
                Array.Copy(hash,hash.Length - subfolder.Length,subfolder,0,subfolder.Length);
                subfolder[0] = (byte)((int)subfolder[0] >> ((subfolder.Length * 8) - bits)); //Set extra bits to 0.
                return Base16Encode(subfolder);
            }
            return null;
        }


        protected string Base16Encode(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x").PadLeft(2, '0'));
            return sb.ToString();
        }

        
        /// <summary>
        /// Generates the resized image to disk (if needed), then rewrites the request to that location.
        /// Perform 404 checking before calling this method. Assumes file exists.
        /// Called during PostAuthorizeRequest
        /// </summary>
        /// <param name="r"></param>
        /// <param name="extension"></param>
        protected virtual void ResizeRequest(HttpContext context, yrl current)
        {

            Stopwatch s = new Stopwatch();
            s.Start();

            //Override normal behavior with virtualpathprovider
            Boolean virtualFile = "true".Equals(ConfigurationManager.AppSettings["ImageResizerUseVirtualPathProvider"], StringComparison.OrdinalIgnoreCase);

            //Fall back mode - only use virtual path provider as a fallback if the file doesn't exist - recommended setting.
            Boolean virtualFileFallBack = "true".Equals(ConfigurationManager.AppSettings["ImageResizerUseVirtualPathProviderAsFallback"], StringComparison.OrdinalIgnoreCase);

            

            VirtualFile vf = virtualFile ? HostingEnvironment.VirtualPathProvider.GetFile(getVPPSafePath(current)) : null;
            
            //In fallback mode, only create vf if the file doesn't exist.
            if (vf == null && virtualFileFallBack) 
                if (!File.Exists(current.Local)) vf = HostingEnvironment.VirtualPathProvider.GetFile(getVPPSafePath(current));
            
                

            //This is where the cached version goes
            string cachedFile = getCachedVersionFilename(current);

            //Find out if we have a modified date that we can work with
            bool hasModifiedDate = (vf == null) || vf is IVirtualFileWithModifiedDate;
            if (hasModifiedDate && vf != null)
            {
                DateTime modDate = ((IVirtualFileWithModifiedDate)vf).ModifiedDateUTC;
                if (modDate == DateTime.MinValue || modDate == DateTime.MaxValue)
                {
                    hasModifiedDate = false; //Skip modified date checking if the file
                }
            }

            //Disk caching is good for images because they change much less often than the application restarts.

            //Make sure the resized image is in the disk cache.
            bool succeeded = DiskCache.UpdateCachedVersionIfNeeded(delegate()
                {
                    if (vf == null)
                    {
                        return System.IO.File.GetLastWriteTimeUtc(current.Local);
                    }
                    else if (hasModifiedDate)
                    {
                        return ((IVirtualFileWithModifiedDate)vf).ModifiedDateUTC;
                    }
                    else
                    {
                        //Won't be called, no modified date available.
                        return DateTime.MinValue;
                    }
                }, 
                cachedFile,
                delegate()
                {

                    //This runs if the update is needed. This delegate is preventing from running in more
                    //than one thread at a time for the specified source file (current.Local)

                    if (vf != null)
                    {   //For VPP use.
                        //Use bitmap interface if available
                        if (vf is IVirtualBitmapFile)
                            ImageManager.getBestInstance().BuildImage((IVirtualBitmapFile)vf, cachedFile, current.QueryString);
                        else
                            ImageManager.getBestInstance().BuildImage(vf, cachedFile, current.QueryString);
                    }
                    else
                    {
                        ImageManager.getBestInstance().BuildImage(current.Local, cachedFile, current.QueryString);
                    }

                }, 30000,
                !hasModifiedDate); //When hasModifiedDate is false, modified dates aren't checked (there aren't any!).

            //If a co-occurring resize has the file locked for more than 30 seconds, quit with an error.
            if (!succeeded)
                throw new ApplicationException("Failed to acquire a lock on file \"" + current.Virtual + "\" within 30 seconds. Image resizing failed.");
            

            //Get domain-relative path of cached file.
            string virtualPath = yrl.GetAppFolderName().TrimEnd(new char[] { '/' }) + "/" + yrl.FromPhysicalString(cachedFile).ToString();

            //Add content-type headers (they're not added correctly when the source URL extension is wrong)
            //Determine content-type string;
            string contentType = new ImageOutputSettings(current).GetContentType();
            
            context.Items["FinalContentType"] = contentType;
            context.Items["FinalCachedFile"] = cachedFile;

            s.Stop();
            context.Items["ResizingTime"] = s.ElapsedMilliseconds;

            //Rewrite to cached, resized image.
            context.RewritePath(virtualPath, false);
        }
        /// <summary>
        /// We don't actually send the data - but we still want to control the headers on the data.
        /// PreSendRequestHeaders allows us to change the content-type and cache headers at excatly the last
        /// second. We populate the headers from context.Items["FinalContentType"],
        /// context.Items["ContentExpires"], and context.Items["FinalCachedFile"].
        /// This also indirectly enables server-side mem caching. (HttpCacheability.Public does it)
        /// We set the file dependency to FinalCachedFile so changes are update quickly server-side
        /// - however, clients will not check for updates until ContentExpires occurs.
        ///  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void context_PreSendRequestHeaders(object sender, EventArgs e)
        {
            HttpApplication app = sender as HttpApplication;
            HttpContext context = (app != null) ? app.Context : null;
            //Check to ensure the context and Response is in good shape (it's needed)
            if (context != null && context.Items != null && context.Items["FinalContentType"] != null && context.Items["FinalCachedFile"] != null)
            {
                //Clear previous output
                //context.Response.Clear();
                context.Response.ContentType = context.Items["FinalContentType"].ToString();
                //Add caching headers
                context.Response.AddFileDependency(context.Items["FinalCachedFile"].ToString());

                //It's not UTC - server time zone.
                if (context.Items["ContentExpires"] != null)
                    context.Response.Cache.SetExpires((DateTime)context.Items["ContentExpires"]);

                //Enables in-memory caching
                context.Response.Cache.SetCacheability(HttpCacheability.Public);
                context.Response.Cache.SetLastModifiedFromFileDependencies();
                context.Response.Cache.SetValidUntilExpires(false);

                 //Uncomment only on Cassini.. Doesn't work anywhere else, could force the managed code path in IIS6 
               /* if (context.Items["ResizingTime"] != null)
                {
                    context.Response.AddHeader("ResizingAndSavingTimeMs", context.Items["ResizingTime"].ToString());
                }*/
                
            }

        }

        
    }
}
