﻿using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Configuration;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Web;

namespace ImageResizer.Plugins.CloudFront {
    /// <summary>
    /// Allows querystrings to be expressed with '/' or ';' instead of '?', allow the querystring to survive the cloudfront guillotine. 
    /// Since IIS can't stand '&' symbols in the path, you have to replace both '?' and '&' with ';'
    /// Later I hope to include control adapters to automate the process.
    /// </summary>
    public class CloudFrontPlugin:IPlugin {

        public CloudFrontPlugin(){}

        Regex r = new Regex("^(?<path>[^=]+\\.[a-zA-Z0-9]+)[/;](?<query>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        Config c;

        protected string redirectThrough = null;
        protected bool redirectPermanent = false;
        public IPlugin Install(Configuration.Config c) {
            this.c = c;
            c.Plugins.add_plugin(this);
            c.Pipeline.PostAuthorizeRequestStart += Pipeline_PostAuthorizeRequestStart;
            c.Pipeline.RewriteDefaults += Pipeline_RewriteDefaults;
            redirectThrough = c.get("cloudfront.redirectthrough", null);
            redirectPermanent = c.get("cloudfront.permanentRedirect", false);
            return this;
        }

        void Pipeline_PostAuthorizeRequestStart(System.Web.IHttpModule sender, System.Web.HttpContext context) {
            if (!TransformCloudFrontUrl(context) && redirectThrough != null) {
                //It wasn't a cloudfront URL. 
                context.Items[c.Pipeline.ModifiedPathKey + ".notcloudfronturl"] = true;
            }
        }

        bool TransformCloudFrontUrl(System.Web.HttpContext context) {
            if (context.Request.Path.LastIndexOf('=') < 0) return false; //Must have an = sign or there is no query

            //With an equal sign in the path, we can look for the sign.
            Match m = r.Match(context.Request.Path);
            if (!m.Success) return false; //Must match regex

            string path = m.Groups["path"].Captures[0].Value;
            if (!c.Pipeline.IsAcceptedImageType(path)) return false; //Must be valid image type

            string query = m.Groups["query"].Captures[0].Value;

            NameValueCollection q = Util.PathUtils.ParseQueryStringFriendly(query.Replace(";", "&"));
            context.Items[c.Pipeline.ModifiedPathKey] = path; //Fix the path.

            context.Items[c.Pipeline.ModifiedPathKey + ".query"] = q; //Save the querystring for later
            return true;
        }

        void Pipeline_RewriteDefaults(System.Web.IHttpModule sender, System.Web.HttpContext context, IUrlEventArgs e) {
            if (redirectThrough != null && context.Items[c.Pipeline.ModifiedPathKey + ".notcloudfronturl"] != null) {
                //It wasn't a cloudfront URL - which means the request didn't come from CloudFront, it came directly from the browser. Perform a redirect, rewriting the querystring appropriately

                string finalPath = redirectThrough + e.VirtualPath + ";";
                foreach (string s in e.QueryString.Keys) {
                    finalPath += HttpUtility.UrlEncode(s) + "=" + HttpUtility.UrlEncode(e.QueryString[s]);
                }

                context.Response.Redirect(finalPath, !redirectPermanent);
                if (redirectPermanent) {
                    context.Response.StatusCode = 301;
                    context.Response.End();
                }
            }

            //Ok, the rest only happens if it is a cloudfront URL. 

            if (context.Items[c.Pipeline.ModifiedPathKey + ".query"] == null) return;

            NameValueCollection q = context.Items[c.Pipeline.ModifiedPathKey + ".query"] as NameValueCollection;

            //Merge overwrite the querystring with everything found in PathInfo. This is the defaults event, so it will still get overwritten by an additional 'real' querystring.
            foreach (string key in q.Keys)
                e.QueryString[key] = q[key];

        }

        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            c.Pipeline.RewriteDefaults -= Pipeline_RewriteDefaults;
            c.Pipeline.PostAuthorizeRequestStart -= Pipeline_PostAuthorizeRequestStart;
            return true;
        }
    }
}
