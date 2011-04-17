﻿using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Resizing;
using System.Drawing;
using ImageResizer.Configuration;

namespace ImageResizer.Plugins.Basic {
    /// <summary>
    /// Implements app-wide size limits on image size
    /// </summary>
    public class SizeLimiting : BuilderExtension, IPlugin {
        public SizeLimiting() {
        }
        protected SizeLimits l = null;
        public IPlugin Install(Config c) {
            //Load SizeLimits

            c.Plugins.AllPlugins.Add(this);
            c.Plugins.ImageBuilderExtensions.Add(this);
            return this;
        }

        public bool Uninstall(Config c) {
            c.Plugins.remove_plugin(this);
            return true;
        }


        protected override RequestedAction PostLayoutImage(ImageState s) {
            base.PostLayoutImage(s);

            SizeF box = s.layout.GetBoundingBox().Size;

            double wFactor = box.Width / l.ImageSize.Width;
            double hFactor = box.Height / l.ImageSize.Height;

            double scaleFactor = wFactor > hFactor ? wFactor : hFactor;
            if (scaleFactor > 1) {
                //The bounding box exceeds the ImageSize. Scale down until it fits.
                s.layout.Scale(1 / scaleFactor, new PointF(0, 0));
            }

            return RequestedAction.None;
        }



    }
}
