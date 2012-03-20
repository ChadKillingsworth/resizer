﻿using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Resizing;
using AForge.Imaging.Filters;
using System.Globalization;
using ImageResizer.Util;
using AForge;
using AForge.Imaging;
using System.Drawing.Imaging;
using System.Drawing;
using ImageResizer.Configuration;
using System.IO;
using ImageResizer.Caching;

namespace ImageResizer.Plugins.RedEye {
    public class DetectionPlugin : BuilderExtension, IPlugin, IQuerystringPlugin {
        public DetectionPlugin() {
        }

        protected Config c;
        public IPlugin Install(Configuration.Config c) {
            c.Plugins.add_plugin(this);
            this.c = c;
            c.Pipeline.PreHandleImage += Pipeline_PreHandleImage;
            return this;
        }

        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            c.Pipeline.PreHandleImage -= Pipeline_PreHandleImage;
            return true;
        }

        class RedEyeData {
            public int xunits;
            public int yunits;
            public float dx;
            public float dy;
            public float dw;
            public float dh;
            public List<ObjRect> features;
        }

        protected override RequestedAction Render(ImageState s) {
            if (!Utils.getBool(s.settings, "r.detecteyes", false)) return RequestedAction.None;


            var ex = new ResizingCanceledException("Resizing was canceled as JSON data was requested instead");

            RedEyeData d = new RedEyeData();
            d.features = new FaceDetection().DetectFeatures(s.sourceBitmap);
            d.xunits = s.originalSize.Width;
            d.yunits = s.originalSize.Height;
            RectangleF dest = PolygonMath.GetBoundingBox(s.layout["image"]);
            d.dx = dest.X;
            d.dy = dest.Y;
            d.dw = dest.Width;
            d.dh = dest.Height;
            ex.ContentType = "application/json; charset=utf-8";
            StringWriter sw = new StringWriter();
            new Newtonsoft.Json.JsonSerializer().Serialize(sw, d);
            ex.ResponseData = UTF8Encoding.UTF8.GetBytes(sw.ToString().ToCharArray());
            ex.StatusCode = 200;

            throw ex;
        }

        /// <summary>
        /// This is where we hijack the resizing process, interrupt it, and send back the json data we created.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="context"></param>
        /// <param name="e"></param>
        void Pipeline_PreHandleImage(System.Web.IHttpModule sender, System.Web.HttpContext context, Caching.IResponseArgs e) {
            if (!Utils.getBool(e.RewrittenQuerystring, "r.detecteyes", false)) return;


            ResponseArgs ra = e as ResponseArgs;
            e.ResponseHeaders.ContentType = "application/json; charset=utf-8";

            var old = ra.ResizeImageToStream;
            ra.ResizeImageToStream = new ResizeImageDelegate(delegate(Stream s) {
                try {
                    old(s);
                } catch (ResizingCanceledException rce) {
                    s.Write(rce.ResponseData, 0, rce.ResponseData.Length);
                }
            });
        }

        public virtual IEnumerable<string> GetSupportedQuerystringKeys() {
            return new string[] { "r.detecteyes"};
        }
    }
}
