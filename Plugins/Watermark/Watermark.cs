﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt for your rights. */
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;
using System.Drawing;
using System.Web;
using ImageResizer.Util;
using ImageResizer.Resizing;
using System.Web.Hosting;
using ImageResizer.Configuration;
using System.Web.Caching;

namespace ImageResizer.Plugins.Watermark
{
    /// <summary>
    /// Provides extensibility points for drawing watermarks and even modifying resizing/image settings
    /// </summary>
    public class WatermarkPlugin : BuilderExtension, IPlugin, IQuerystringPlugin
    {
        public string watermarkDir = "~/watermarks";
        public SizeF watermarkSize = new SizeF(1, 1);
        public Boolean valuesPercentages = true;
        public Boolean keepAspectRatio = true;
        public SizeF topLeftPadding = new SizeF(0, 0);
        public SizeF bottomRightPadding = new SizeF(0, 0);
        public Boolean hideIfTooSmall = false;
        public System.Drawing.ContentAlignment align = ContentAlignment.MiddleCenter;
 

        public WatermarkPlugin()
        {
        }


        /// <summary>
        /// Loads a bitmap, cached using asp.net's cache
        /// </summary>
        /// <param name="localfile"></param>
        /// <returns></returns>
        public Bitmap GetMemCachedBitmap(string virtualPath)
        {
            string key = virtualPath.ToLowerInvariant();
            Bitmap b = HttpContext.Current.Cache[key] as Bitmap;
            if (b != null) return b;

            b = ImageBuilder.Current.LoadImage(virtualPath, new ResizeSettings());
            //Query VPPs for cache dependency. TODO: Add support for IVirtualImageProviders to customize cache dependencies.
            CacheDependency cd = null;
            if (HostingEnvironment.VirtualPathProvider != null) cd = HostingEnvironment.VirtualPathProvider.GetCacheDependency(virtualPath, new string[] { }, DateTime.UtcNow);

            HttpContext.Current.Cache.Insert(key, b,cd);
            return b;
        }


        protected override RequestedAction RenderOverlays(ImageState s) {
            string watermark = s.settings["watermark"]; //from the querystring
            Graphics g = s.destGraphics;
            RectangleF imageBox = PolygonMath.GetBoundingBox(s.layout["image"]);

            if (string.IsNullOrEmpty(watermark)) return RequestedAction.None;

            if (watermark.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) > -1 ||
                watermark.IndexOfAny(new char[] { '\\', '/' }) > -1)
                throw new ArgumentException("Watermark value contained invalid file name characters: " + watermark);

            //Combine the directory with the base dir
            watermark = watermarkDir.TrimEnd('/') + '/' + watermark.TrimStart('/');

            //Verify the file exists. If the watermark doesn't exist, skip watermarking.
            if (!c.Pipeline.FileExists(watermark, s.settings)) return RequestedAction.None;

            //Load the file specified in the querystring,
            Bitmap wb = GetMemCachedBitmap(watermark);
            lock(wb){
                //If percentages, resolve to pixels
                if (valuesPercentages) {
                    watermarkSize.Height *= imageBox.Height;
                    watermarkSize.Width *= imageBox.Width;
                    topLeftPadding.Height *= imageBox.Height;
                    topLeftPadding.Width *= imageBox.Width;
                    bottomRightPadding.Height *= imageBox.Height;
                    bottomRightPadding.Width *= imageBox.Width;
                }

                //Keep aspect ratio
                if (keepAspectRatio) watermarkSize = PolygonMath.ScaleInside(wb.Size, watermarkSize);


                //Floor all values
                watermarkSize = new SizeF((float)Math.Floor(watermarkSize.Width), (float)Math.Floor(watermarkSize.Height));
                topLeftPadding = new SizeF((float)Math.Floor(topLeftPadding.Width), (float)Math.Floor(topLeftPadding.Height));
                bottomRightPadding = new SizeF((float)Math.Floor(bottomRightPadding.Width), (float)Math.Floor(bottomRightPadding.Height));


                //Check boundingbox
                SizeF watermarkBoundingBox = new SizeF(watermarkSize.Width + topLeftPadding.Width + bottomRightPadding.Width,
                    watermarkSize.Height + topLeftPadding.Height + bottomRightPadding.Height);

                //Don't draw the watermark if it is too small.
                if (!PolygonMath.FitsInside(watermarkBoundingBox, imageBox.Size)) {
                    if (hideIfTooSmall) return RequestedAction.None;
                    else {
                        SizeF oldSize = watermarkBoundingBox;
                        watermarkBoundingBox = PolygonMath.ScaleInside(watermarkBoundingBox, imageBox.Size);
                        watermarkSize.Width -= (oldSize.Width - watermarkBoundingBox.Width);
                        watermarkSize.Height -= (oldSize.Height - watermarkBoundingBox.Height);
                    }
                }
                //Floor all values again
                watermarkSize = new SizeF((float)Math.Floor(watermarkSize.Width), (float)Math.Floor(watermarkSize.Height));
                topLeftPadding = new SizeF((float)Math.Floor(topLeftPadding.Width), (float)Math.Floor(topLeftPadding.Height));
                bottomRightPadding = new SizeF((float)Math.Floor(bottomRightPadding.Width), (float)Math.Floor(bottomRightPadding.Height));



                float innerWidth = (float)Math.Floor(imageBox.Width - Math.Abs(topLeftPadding.Width) - Math.Abs(bottomRightPadding.Width));
                float innerHeight = (float)Math.Floor(imageBox.Height - Math.Abs(topLeftPadding.Height) - Math.Abs(bottomRightPadding.Height));

                float x = 0;
                float y = 0;

                if (align == ContentAlignment.BottomCenter || align == ContentAlignment.BottomLeft || align == ContentAlignment.BottomRight)
                    y = (innerHeight - watermarkSize.Height) + topLeftPadding.Height;

                if (align == ContentAlignment.MiddleCenter || align == ContentAlignment.MiddleLeft || align == ContentAlignment.MiddleRight)
                    y = (innerHeight - watermarkSize.Height) / 2 + topLeftPadding.Height;

                if (align == ContentAlignment.TopCenter || align == ContentAlignment.TopLeft || align == ContentAlignment.TopRight)
                    y = topLeftPadding.Height;


                if (align == ContentAlignment.BottomRight || align == ContentAlignment.MiddleRight || align == ContentAlignment.TopRight)
                    x = (innerWidth - watermarkSize.Width) + topLeftPadding.Width;

                if (align == ContentAlignment.BottomCenter || align == ContentAlignment.MiddleCenter || align == ContentAlignment.TopCenter)
                    x = (innerWidth - watermarkSize.Width) / 2 + topLeftPadding.Width;

                if (align == ContentAlignment.BottomLeft || align == ContentAlignment.MiddleLeft || align == ContentAlignment.TopLeft)
                    x = topLeftPadding.Width;

                //Draw watermark
                g.DrawImage(wb, new Rectangle((int)(x + imageBox.X), (int)(y + imageBox.Y), (int)watermarkSize.Width, (int)watermarkSize.Height));

            }

            return RequestedAction.None;
        }

        Config c;
        public IPlugin Install(Configuration.Config c) {
            c.Plugins.add_plugin(this);
            this.c = c;
            return this;
        }

        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            return true;
        }

        public IEnumerable<string> GetSupportedQuerystringKeys() {
            return new string[] { "watermark" };
        }
    }
}
