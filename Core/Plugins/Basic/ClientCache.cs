/* Copyright (c) 2011 Nathanael Jones. See license.txt */
using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Configuration;

// All plugins in this namespace are licensed under the Freedom license
namespace ImageResizer.Plugins.Basic {
    /// <summary>
    /// Provides default client-caching behavior. Sends Last-Modified header if present, and Expires header if &lt;clientcache minutes="value" /&gt; is configured.
    /// Also defaults Cache-control to Public for anonymous requests (and private for authenticated requests)
    /// </summary>
    public class ClientCache:IPlugin, IQuerystringPlugin {

        Config c;
        public IPlugin Install(Configuration.Config c) {
            this.c = c;
            c.Plugins.add_plugin(this);
            c.Pipeline.PreHandleImage += Pipeline_PreHandleImage; 
            return this;
        }

        /*  http://developer.yahoo.com/performance/rules.html
            http://24x7aspnet.blogspot.com/2009/06/using-cache-methods-httpcacheability-in.html

            Redirects should have caching headers.
            Expires: is good
            Remove ETags, bad server implementation
         */

        void Pipeline_PreHandleImage(System.Web.IHttpModule sender, System.Web.HttpContext context, Caching.IResponseArgs e) {
            Instructions instructions = new Instructions(context.Request.QueryString);
            int maxage = instructions.Get<int>("max-age", -1);
            int mins = c.get("clientcache.minutes", -1);
            
            //Set the expires value if present
            //Use the querystring provided value as an override for the configuration default
            if (maxage >= 0) {
                // Convert a max-age value (timespan in seconds) to the equivalent expires value
                e.ResponseHeaders.Expires = DateTime.UtcNow.AddSeconds(maxage);
            } else if (mins > 0) {
                e.ResponseHeaders.Expires = DateTime.UtcNow.AddMinutes(mins);
            }

            //NDJ Jan-16-2013. The last modified date sent in the headers should NOT match the source modified date when using DiskCaching.
            //Setting this will prevent 304s from being sent properly.
            // (Moved to NoCache)
     
            //Authenticated requests only allow caching on the client. 
            //Anonymous requests get caching on the server, proxy and client
            if (context.Request.IsAuthenticated)
                e.ResponseHeaders.CacheControl = System.Web.HttpCacheability.Private;
            else
                e.ResponseHeaders.CacheControl = System.Web.HttpCacheability.Public;
        }

        public IEnumerable<string> GetSupportedQuerystringKeys() {
            return new string[] { "max-age" };
        }

        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            c.Pipeline.PreHandleImage -= Pipeline_PreHandleImage;
            return true;
        }
    }
}
