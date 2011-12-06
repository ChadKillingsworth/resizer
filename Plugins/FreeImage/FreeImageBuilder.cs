﻿using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Resizing;
using ImageResizer.Encoding;
using ImageResizer.Configuration.Issues;
using FreeImageAPI;
using System.Drawing;
using ImageResizer.Util;
using System.Drawing.Imaging;
using ImageResizer.Plugins.Basic;
using System.IO;
using ImageResizer.Plugins.FreeImageEncoder;
using ImageResizer.Plugins.FreeImageScaling;
using ImageResizer.Configuration;
using System.Web.Hosting;

namespace ImageResizer.Plugins.FreeImageBuilder {
    public class FreeImageBuilderPlugin :BuilderExtension, IPlugin, IIssueProvider {

        public FreeImageBuilderPlugin(){
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

        /// <summary>
        /// Adds alternate pipeline based on FreeImage. Invoked by &builder=freeimage. 
        /// This method doesn't handle job.DisposeSource or job.DesposeDest or settings filtering, that's handled by ImageBuilder.
        /// All the bitmap processing is handled by buildFiBitmap, this method handles all the I/O
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        protected override RequestedAction BuildJob(ImageJob job) {
            if (!"freeimage".Equals(job.Settings["builder"])) return RequestedAction.None;
            if (!FreeImageAPI.FreeImage.IsAvailable()) return RequestedAction.None;

            //StringBuilder log = new StringBuilder();

            //FreeImageAPI.FreeImageEngine.Message += (delegate(FREE_IMAGE_FORMAT fmt, string msg) {
            //    log.AppendLine(msg);
            //});

            // Variables
            Stream s = null;
            bool disposeStream = !(job.Source is Stream);
            long originalPosition = 0;
            bool restoreStreamPosition = false;
            try{
                //Get a Stream instance for the job
                string path;
                s = c.CurrentImageBuilder.GetStreamFromSource(job.Source, job.Settings, ref disposeStream, out path, out restoreStreamPosition);
                if (s == null) return  RequestedAction.None;
                if (job.ResetSourceStream) restoreStreamPosition = true;
                job.SourcePathData = path;
            
                //Save the original stream positione
                originalPosition = (restoreStreamPosition) ? s.Position : - 1;

                FIBITMAP b = FIBITMAP.Zero;
                try {
                    //What is our destination format
                    IEncoder managedEncoder = c.Plugins.GetEncoder(job.Settings, job.SourcePathData); //Use the existing pipeline to parse the querystring
                    //FREE_IMAGE_FORMAT destFormat = FreeImage.GetFIFFromMime(managedEncoder.MimeType); //Use the resulting mime-type to determine the output format.
                    //This prevents us from supporting output formats that don't already have registered encoders. Good, right?

                    bool supportsTransparency = managedEncoder.SupportsTransparency;
                    //Do all the bitmap stuff in another method
                    b = buildFiBitmap(s, job, supportsTransparency);
                    if (b.IsNull) return RequestedAction.None;

                    // Try to save the bitmap
                    if (job.Dest is string || job.Dest is Stream) {
                        FreeImageEncoderPlugin e = new FreeImageEncoderPlugin(job.Settings, path);
                        if (job.Dest is string) {
                            string destPath = job.Dest as string;
                            //Convert app-relative paths
                            if (destPath.StartsWith("~", StringComparison.OrdinalIgnoreCase)) destPath = HostingEnvironment.MapPath(destPath);

                            //Add the file extension if specified.
                            if (job.AddFileExtension) {
                                if (e != null) destPath += "." + e.Extension; //NOTE: These may differ from the normal default extensions
                            }
                            job.FinalPath = destPath;
                            if (!FreeImage.Save(e.Format, b, destPath, e.EncodingOptions)) return RequestedAction.None;
                        } else if (job.Dest is Stream) {
                            if (!FreeImage.SaveToStream(b, (Stream)job.Dest, e.Format, e.EncodingOptions)) return RequestedAction.None;
                        }
                    } else if (job.Dest == typeof(Bitmap)) {
                        job.Result = FreeImage.GetBitmap(b);
                    } else return RequestedAction.None;
                } finally {
                    //Ensure we unload the resulting bitmap
                   if (!b.IsNull) FreeImage.UnloadEx(ref b);
                }
                return RequestedAction.Cancel;
            }finally{
                if (s != null && restoreStreamPosition && s.CanSeek) s.Seek(originalPosition, SeekOrigin.Begin);
                if (disposeStream) s.Dispose();
            }

        }
        /// <summary>
        /// Builds an FIBitmap from the stream and job.Settings 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        protected FIBITMAP buildFiBitmap(Stream s, ImageJob job, bool supportsTransparency){

            ResizeSettings settings = job.Settings;
            FIBITMAP original = FreeImage.LoadFromStream((Stream)s);
            if (original.IsNull) return FIBITMAP.Zero;
            FIBITMAP final = FIBITMAP.Zero;
            
            try{
                //Find the image size
                Size orig = new Size( (int)FreeImage.GetWidth(original),  (int)FreeImage.GetHeight(original));

                //Calculate the new size of the image and the canvas.
                ImageState state = new ImageState(settings, orig, true);
                c.CurrentImageBuilder.Process(state);
                RectangleF imageDest = PolygonMath.GetBoundingBox(state.layout["image"]);

                if (imageDest.Width != orig.Width || imageDest.Height != orig.Height) {
                    //Rescale
                    bool temp;
                    final = FreeImage.Rescale(original, (int)imageDest.Width, (int)imageDest.Height, FreeImageScalingPlugin.ParseResizeAlgorithm(settings["fi.scale"], FREE_IMAGE_FILTER.FILTER_BOX, out temp));
                    FreeImage.UnloadEx(ref original);
                    if (final.IsNull) return FIBITMAP.Zero;
                } else {
                    final = original;
                }

                RGBQUAD bgcolor = default(RGBQUAD);
                bgcolor.Color = settings.BackgroundColor;
                if (settings.BackgroundColor == Color.Transparent && !supportsTransparency)
                    bgcolor.Color = Color.White;

                //If we need to leave padding, do so.
                BoxPadding outsideImage = new BoxPadding(imageDest.Left, imageDest.Top, state.destSize.Width - imageDest.Right, state.destSize.Height - imageDest.Bottom);

                if (outsideImage.All != 0) {
                    original = final;
                    //Extend canvas
                    final = FreeImage.EnlargeCanvas<RGBQUAD>(original,
                                (int)outsideImage.Left, (int)outsideImage.Top, (int)outsideImage.Right, (int)outsideImage.Bottom, 
                                bgcolor.Color != Color.Transparent ? new Nullable<RGBQUAD>(bgcolor) : null,
                                FREE_IMAGE_COLOR_OPTIONS.FICO_RGBA);
 
                    FreeImage.UnloadEx(ref original);
                    if (final.IsNull) return FIBITMAP.Zero;
                }

                return final;
            
            }finally{
                //Ensure we unload the source bitmap (unless it's also the final one)
                if (original != final && !original.IsNull) FreeImage.UnloadEx(ref original);
            }
        }


        public IEnumerable<IIssue> GetIssues() {
            List<IIssue> issues = new List<IIssue>();
            if (!FreeImageAPI.FreeImage.IsAvailable()) issues.Add(new Issue("The FreeImage library is not available! All FreeImage plugins will be disabled.", IssueSeverity.Error));
            return issues;
        }
    }
}
