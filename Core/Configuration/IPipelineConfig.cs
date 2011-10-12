﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt */
using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Caching;
using ImageResizer.Configuration;
using System.Collections.Specialized;
using System.Web;
using ImageResizer.Plugins;

namespace ImageResizer.Configuration {
    public enum VppUsageOption {
        Fallback, Never, Always
    }


    public delegate void RequestEventHandler(IHttpModule sender, HttpContext context);
    public delegate void UrlRewritingEventHandler(IHttpModule sender, HttpContext context, IUrlEventArgs e);
    public delegate void UrlEventHandler(IHttpModule sender, HttpContext context, IUrlEventArgs e);
    public delegate void UrlAuthorizationEventHandler(IHttpModule sender, HttpContext context, IUrlAuthorizationEventArgs e);
    public delegate void PreHandleImageEventHandler(IHttpModule sender, HttpContext context, IResponseArgs e);
    public delegate void CacheSelectionHandler(object sender, ICacheSelectionEventArgs e);


    public interface IPipelineConfig:IVirtualImageProvider {
        /// <summary>
        /// True if the specified extension is one that the pipeline can handle
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        bool IsAcceptedImageType(string filePath);
        /// <summary>
        /// True if the querystring contains any directives that are understood by the pipeline
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        bool HasPipelineDirective(NameValueCollection q);

        /// <summary>
        /// The key in Context.Items to store the modified querystring (i.e, post-rewrite). 
        /// Allows VirtualPathProviders to access the rewritten data.
        /// </summary>
        string ModifiedQueryStringKey { get;  }

        /// <summary>
        /// The key in Context.Items to store the IResponseArgs object
        /// </summary>
        string ResponseArgsKey { get; }

        /// <summary>
        /// The key in Context.Items to set if we want to cancel MVC routing for the request
        /// </summary>
        string StopRoutingKey { get; }

        /// <summary>
        ///  The key in Context.Items to access a the path to use instead of Request.path
        /// </summary>
         string ModifiedPathKey { get; }
        /// <summary>
        /// The behavior to use when accessing the file system.
        /// </summary>
        VppUsageOption VppUsage { get; }

        string SkipFileTypeCheckKey { get; }
        bool SkipFileTypeCheck { get; }

        /// <summary>
        /// Returns the value of Context.Items["resizer.newPath"] if present. If not, returns FilePath + PathInfo.
        /// Sets Context.Items["resizer.newPath"]. 
        /// Only useful during the Pipeline.PostAuthorizeRequestStart event.
        /// </summary>
        string PreRewritePath { get; }


        /// <summary>
        /// Removes the first fake extension detected at the end of 'path' (like image.jpg.ashx -> image.jpg).
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        string TrimFakeExtensions(string path);

        /// <summary>
        /// Returns an ImageBuilder instance to use for image processing.
        /// </summary>
        /// <returns></returns>
        ImageBuilder GetImageBuilder();

        /// <summary>
        /// Returns a ICacheProvider instance that provides caching system selection and creation.
        /// </summary>
        /// <returns></returns>
        ICacheProvider GetCacheProvider();

		/// <summary>
        /// Returns an IVirtualFile instance if the specified file exists.
		/// </summary>
		/// <param name="virtualPath"></param>
		/// <param name="queryString"></param>
		/// <returns></returns>
        IVirtualFile GetFile(string virtualPath, NameValueCollection queryString);

        /// <summary>
        /// Returns true if (a) A registered IVirtualImageProvider says it exists, or (b) if the VirtualPathProvider chain says it exists.
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <param name="queryString"></param>
        /// <returns></returns>
        bool FileExists(string virtualPath, NameValueCollection queryString);


        void FirePostAuthorizeRequest(IHttpModule sender, System.Web.HttpContext httpContext);

        void FireRewritingEvents(IHttpModule sender, System.Web.HttpContext context, IUrlEventArgs ue);

        void FireAuthorizeImage(IHttpModule sender, System.Web.HttpContext context, IUrlAuthorizationEventArgs urlEventArgs);

        void FirePreHandleImage(IHttpModule sender, System.Web.HttpContext context, IResponseArgs e);


        void FireImageMissing(IHttpModule sender, System.Web.HttpContext context, IUrlEventArgs urlEventArgs);


        NameValueCollection ModifiedQueryString { get; set; }
    }
}
