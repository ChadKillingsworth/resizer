﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;
using ImageResizer.Configuration;
using System.Net;
using ImageResizer.Util;
using System.Security.Cryptography;
using ImageResizer.Configuration.Issues;
using System.IO;
using ImageResizer.Resizing;

namespace ImageResizer.Plugins.RemoteReader {

    public class RemoteReaderPlugin : BuilderExtension, IPlugin, IVirtualImageProvider, IIssueProvider {

        private static string base64UrlKey = "urlb64";

        public static string Base64UrlKey {
            get { return RemoteReaderPlugin.base64UrlKey; }
        }
        private static string hmacKey = "hmac";

        public static string HmacKey {
            get { return RemoteReaderPlugin.hmacKey; }
        }

        protected string remotePrefix = "~/remote";
        Config c;
        public RemoteReaderPlugin() {
            remotePrefix = Util.PathUtils.ResolveAppRelative(remotePrefix);
        }

        public IPlugin Install(Configuration.Config c) {
            this.c = c;
            c.Plugins.add_plugin(this);
            c.Pipeline.RewriteDefaults += Pipeline_RewriteDefaults;
            c.Pipeline.PostRewrite += Pipeline_PostRewrite;
            return this;
        }


        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            c.Pipeline.RewriteDefaults -= Pipeline_RewriteDefaults;
            c.Pipeline.PostRewrite -= Pipeline_PostRewrite;
            return true;
        }

        /// <summary>
        /// Allows .Build and .LoadImage to resize remote URLs
        /// </summary>
        /// <param name="source"></param>
        /// <param name="settings"></param>
        protected override void PreLoadImage(ref object source,ref string path, ref bool disposeSource, ref  ResizeSettings settings) {
            //Turn remote URLs into URI instances
            if (source is string && (((string)source).StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                    ((string)source).StartsWith("https://", StringComparison.OrdinalIgnoreCase))){
                if (Uri.IsWellFormedUriString((string)source, UriKind.Absolute))
                    source = new Uri((string)source);

            }
            //Turn URI instances into streams
            if (source is Uri) {
                path = ((Uri)source).ToString();
                source = GetUriStream((Uri)source);
            }
        }


        void Pipeline_RewriteDefaults(System.Web.IHttpModule sender, System.Web.HttpContext context, IUrlEventArgs e) {
            //Set the XXX of /remote.XXX to the real extension used by the remote file.
            //Allows the output extension and mime-type default to be determined correctly
            if (IsRemotePath(e.VirtualPath) &&
                    !string.IsNullOrEmpty(e.QueryString[Base64UrlKey])) {
                string ext = PathUtils.GetExtension(PathUtils.FromBase64UToString(e.QueryString[Base64UrlKey]));
                e.VirtualPath = PathUtils.SetExtension(e.VirtualPath, ext);
            }
        }

        void Pipeline_PostRewrite(System.Web.IHttpModule sender, System.Web.HttpContext context, IUrlEventArgs e) {
            if (IsRemotePath(e.VirtualPath)) {
                //Force images to be processed - don't allow them to only cache it.
                e.QueryString["process"] = ProcessWhen.Always.ToString();
            }
        }


        /// <summary>
        /// Returns the currently registered RemoteReaderPlugin, or adds a new RemoteReaderPlugin automatically if one is not registered.
        /// 
        /// </summary>
        public static RemoteReaderPlugin Current {
            get {
                RemoteReaderPlugin p = Config.Current.Plugins.Get<RemoteReaderPlugin>();
                if (p != null) return p;
                return (RemoteReaderPlugin)new RemoteReaderPlugin().Install(Config.Current);
            }
        }

        /// <summary>
        /// Generates a signed domain-relative URL in the form "/app/remote.jpg.ashx?width=200&urlb64=aHnSh3haSh...&hmac=913f3KJGK3hj"
        /// </summary>
        /// <param name="remoteUrl"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public string CreateSignedUrl(string remoteUrl, NameValueCollection settings) {
            settings[Base64UrlKey] = PathUtils.ToBase64U(remoteUrl);
            settings[HmacKey] = SignData(settings[Base64UrlKey]);
            return remotePrefix + ".jpg.ashx" + PathUtils.BuildQueryString(settings);
        }

        public string CreateSignedUrl(string remoteUrl, string settings) {
            return CreateSignedUrl(remoteUrl, new ResizeSettings(settings));
        }
        public string SignData(string data) {

            string key = c.get("remoteReader.signingKey", String.Empty);
            if (string.IsNullOrEmpty(key)) throw new ImageResizer.ImageProcessingException("You are required to set a passphrase for securing remote URLs. <resizer><remotereader signingKey=\"put a long and randam passphrase here\" /> </resizer>");

            HMACSHA256 hmac = new HMACSHA256(UTF8Encoding.UTF8.GetBytes(key));
            byte[] hash = hmac.ComputeHash(UTF8Encoding.UTF8.GetBytes(data));
            //32-byte hash is a bit overkill. Truncation doesn't weaking the integrity of the algorithm.
            byte[] shorterHash = new byte[8];
            Array.Copy(hash, shorterHash, 8);
            return PathUtils.ToBase64U(shorterHash);
        }

        public RemoteRequestEventArgs ParseRequest(string virtualPath, NameValueCollection query) {
            if (!IsRemotePath(virtualPath)) return null;

            RemoteRequestEventArgs args = new RemoteRequestEventArgs();
            args.SignedRequest = false;
            args.QueryString = query;

            if (!string.IsNullOrEmpty(query[Base64UrlKey])) {
                string data = query[Base64UrlKey];
                string hmac = query[HmacKey];
                query.Remove(Base64UrlKey);
                query.Remove(HmacKey);
                if (!SignData(data).Equals(hmac))
                    throw new ImageProcessingException("Invalid request! This request was not properly signed, or has been tampered with since transmission.");
                args.RemoteUrl = PathUtils.FromBase64UToString(data);
                args.SignedRequest = true;
            } else
                args.RemoteUrl = "http://" + virtualPath.Substring(remotePrefix.Length).TrimStart('/', '\\');

            if (!Uri.IsWellFormedUriString(args.RemoteUrl, UriKind.Absolute))
                throw new ImageProcessingException("Invalid request! The specified Uri is invalid: " + args.RemoteUrl);
            return args;
        }


        public bool IsRemotePath(string virtualPath) {
            return (virtualPath.StartsWith(remotePrefix, StringComparison.OrdinalIgnoreCase));
        }

        public bool FileExists(string virtualPath, System.Collections.Specialized.NameValueCollection queryString) {
            return IsRemotePath(virtualPath);
        }

        public event RemoteRequest AllowRemoteRequest;

        public IVirtualFile GetFile(string virtualPath, System.Collections.Specialized.NameValueCollection queryString) {
            RemoteRequestEventArgs request = ParseRequest(virtualPath, queryString);
            if (request == null) throw new FileNotFoundException();

            if (request.SignedRequest && c.get("remotereader.allowAllSignedRequests", false)) request.DenyRequest = false;

            //Fire event
            if (AllowRemoteRequest != null) AllowRemoteRequest(this, request);

            if (request.DenyRequest) throw new ImageProcessingException(403, "The specified remote URL is not permitted.");

            return new RemoteSiteFile(virtualPath, request, this);
        }

        public IEnumerable<IIssue> GetIssues() {
            List<IIssue> issues = new List<IIssue>();
            string key = c.get("remoteReader.signingKey", String.Empty);
            if (string.IsNullOrEmpty(key))
                issues.Add(new Issue("You are required to set a passphrase for securing remote URLs. Example: <resizer><remotereader signingKey=\"put a long and randam passphrase here\" /> </resizer>"));
            return issues;

        }


        public Stream GetUriStream(Uri uri) {

            HttpWebResponse response = null;
            try {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = 15000; //Default to 15 seconds. Browser timeout is usually 30.

                //This is IDisposable, but only disposes the stream we are returning. So we can't dispose it, and don't need to
                response = request.GetResponse() as HttpWebResponse;
                return response.GetResponseStream();
            } catch {
                if (response != null) response.Close();
                throw;
            }
        }
    }

    public class RemoteSiteFile : IVirtualFile {

        protected string virtualPath;
        protected RemoteReaderPlugin parent;
        private RemoteRequestEventArgs request;

        public RemoteSiteFile(string virtualPath, RemoteRequestEventArgs request, RemoteReaderPlugin parent) {
            this.virtualPath = virtualPath;
            this.request = request;
            this.parent = parent;
        }

        public string VirtualPath {
            get { return virtualPath; }
        }

        public System.IO.Stream Open() {
            return parent.GetUriStream(new Uri(this.request.RemoteUrl));
        }
    }
}
