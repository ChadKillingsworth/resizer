﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using ImageResizer.Resizing;
using ImageResizer.Util;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ImageResizer.Plugins.SeamCarving {
    public class SeamCarvingPlugin : BuilderExtension, IQuerystringPlugin, IPlugin {

        CairManager cair = new CairManager();
        public SeamCarvingPlugin() {

        }
        public enum FilterType {
            None = 10,
            Prewitt = 0,
            V1 = 1,
            VSquare = 2,
            Sobel = 3,
            Laplacian = 4
        }

        public enum OutputType {
            CAIR=  0,
            Grayscale =  1,
            Edge=  2,
            VerticalEnergy =  3,
            HorizontalEnergy =  4,
            Removal= 5,
            CAIR_HD=  6

        }
        public enum EnergyType {
            Backward = 0,
            Forward = 1
        }
              

        protected override RequestedAction PreRenderImage(ImageState s) {
            //Skip this when we are doing simulations
            if (s.destGraphics == null) return RequestedAction.None;

            FilterType ftype = Utils.parseEnum<FilterType>(s.settings["carve"], FilterType.None);
            if ("true".Equals(s.settings["carve"], StringComparison.OrdinalIgnoreCase)) ftype = FilterType.Prewitt;
            if (string.IsNullOrEmpty(s.settings["carve"]) && s.settings.Mode == FitMode.Carve) ftype = FilterType.Prewitt;

            if (ftype == FilterType.None) return RequestedAction.None; //Only override rendering when carving is requested.

            //The minimum dimensions of the temporary bitmap.
            SizeF targetSize = PolygonMath.getParallelogramSize(s.layout["image"]);
            targetSize = new SizeF((float)Math.Ceiling(targetSize.Width), (float)Math.Ceiling(targetSize.Height));

  
            //The size of the temporary bitmap. 
            //We want it larger than the size we'll use on the final copy, so we never upscale it
            //- but we also want it as small as possible so processing is fast.
            SizeF tempSize = PolygonMath.ScaleOutside(targetSize, s.copyRect.Size);
            int tempWidth = (int)Math.Ceiling(tempSize.Width);
            int tempHeight = (int)Math.Ceiling(tempSize.Height);

            //The intermediate and seam carved files
            string tempFile = Path.GetTempFileName();
            string outputTempFile = Path.GetTempFileName();

            try {
                try {

                    //Create a temporary bitmap that is 'halfway resized', so we can efficiently perfom seam carving.

                    //Unless it's already been done for us by FreeImageResize or something
                    if (s.preRenderBitmap != null && (tempWidth - s.preRenderBitmap.Width < 50 && tempHeight - s.preRenderBitmap.Height < 50)) {
                        s.preRenderBitmap.Save(tempFile, ImageFormat.Bmp);
                    } else {
                        //Create the temporary bitmap and graphics.
                        using (Bitmap temp = new Bitmap(tempWidth, tempHeight, PixelFormat.Format32bppArgb))
                        using (Graphics g = Graphics.FromImage(temp))
                        using (ImageAttributes ia = new ImageAttributes()) {
                            //High quality everthing
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                            g.CompositingMode = CompositingMode.SourceOver;
                            ia.SetWrapMode(WrapMode.TileFlipXY);
                            if (s.preRenderBitmap != null) {
                                g.DrawImage(s.preRenderBitmap, new Rectangle(0, 0, tempWidth, tempHeight), 0, 0, s.preRenderBitmap.Width, s.preRenderBitmap.Height, GraphicsUnit.Pixel, ia);
                            } else {
                                g.DrawImage(s.sourceBitmap, new Rectangle(0, 0, tempWidth, tempHeight), s.copyRect.X, s.copyRect.Y, s.copyRect.Width, s.copyRect.Height, GraphicsUnit.Pixel, ia);
                            }
                            g.Flush(FlushIntention.Flush);
                            //Save
                            temp.Save(tempFile, ImageFormat.Bmp);
                        }
                    }

                    Size intTargetSize = new Size((int)targetSize.Width, (int)targetSize.Height);
                    CairJob job = new CairJob();
                    job.SourcePath = tempFile;
                    job.DestPath = outputTempFile;
                    job.Size = intTargetSize;
                    job.Filter = ftype;
                    job.Timeout = 5000;
                    cair.CairyIt(job);

                } finally {
                    File.Delete(tempFile);
                }

                //Dispose old intermediate bitmap first
                if (s.preRenderBitmap != null) s.preRenderBitmap.Dispose();

                //Load the new intermediate file from disk
                s.preRenderBitmap = new Bitmap(outputTempFile);
                s.preRenderBitmap.MakeTransparent();

            } finally {
                File.Delete(outputTempFile);
            }

            return RequestedAction.Cancel;
        }



        public IEnumerable<string> GetSupportedQuerystringKeys() {
            return new string[] { "carve" };
        }

        public IPlugin Install(Configuration.Config c) {
            c.Plugins.add_plugin(this);
            return this;
        }



        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            return true;
        }
    }
}
