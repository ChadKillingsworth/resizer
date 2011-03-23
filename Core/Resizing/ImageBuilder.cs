/**
 * Written by Nathanael Jones 
 * http://nathanaeljones.com
 * nathanael.jones@gmail.com
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
 * 
 **/

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
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Reflection;
using fbs.ImageResizer.Resizing;

namespace fbs.ImageResizer
{
    /// <summary>
    /// Provides methods for generating resizied images, and for reading and writing them to disk.
    /// Use ImageManager.Instance to get the default instance, or use ImageManager.Instance.Create() to control which extensions are used.
    /// </summary>
    public class ImageBuilder : AbstractImageProcessor
    {
        private static ImageBuilder _bestInstance = null;
        private static object _bestInstanceSync = new object();
        /// <summary>
        /// Allows subclasses to be used instead of ImageManager. Replacements must override the Create method and call their own constructor instead.
        /// </summary>
        /// <param name="replacement"></param>
        public static void UpgradeInstance(ImageBuilder replacement){
            lock(_bestInstanceSync) _bestInstance = replacement;
        }
        
        public static ImageBuilder getBestInstance() { return Instance;}

        /// <summary>
        /// Returns a shared instance of ImageManager, or a subclass if it has been upgraded
        /// </summary>
        /// <returns></returns>
        public static ImageBuilder Instance
        {
            get {
                if (_bestInstance == null)
                    lock (_bestInstanceSync)
                        if (_bestInstance == null)
                            _bestInstance = new ImageBuilder(Configuration.GetImageManagerExtensions());

                return _bestInstance;
            }
        }

        /// <summary>
        /// Create a new instance of ImageBuilder using the specified extensions. Extension methods will be fired in the order they exist in the collection.
        /// </summary>
        /// <param name="extensions"></param>
        public ImageBuilder(IEnumerable<ImageBuilderExtension> extensions) {
            base.exts = new List<AbstractImageProcessor>();
            if (extensions != null) foreach (ImageBuilderExtension e in extensions) ((List<AbstractImageProcessor>)exts).Add(e);
        }

        /// <summary>
        /// Create a new instance of ImageBuilder using the specified extensions. Extension methods will be fired in the order they exist in the collection.
        /// </summary>
        /// <param name="extensions"></param>
        public ImageBuilder(IEnumerable<AbstractImageProcessor> extensions): base(extensions){
           
        }
        /// <summary>
        /// Creates another instance of the class using the specified extensions. Subclasses should override this and point to their own constructor.
        /// </summary>
        /// <param name="extensions"></param>
        /// <returns></returns>
        public virtual ImageBuilder Create(IEnumerable<ImageBuilderExtension> extensions) {
            return new ImageBuilder(extensions);
        }

        public virtual ImageBuilder Copy(){
            return new ImageBuilder(exts);
        }


        /// <summary>
        /// Allows Bitmap Build(object) to wrap void Build(object,object) easily.
        /// </summary>
        protected class BitmapHolder {
            public BitmapHolder() { }
            public Bitmap bitmap;
        }
        /// <summary>
        /// Loads a Bitmap from the specified source. If a filename is available, it will be attached to bitmap.Tag. The filename may be virtual, relative, UNC, windows, or unix. 
        /// </summary>
        /// <param name="source">May  be an instance of string, VirtualFile, IVirtualBitmapFile, HttpPostedFile, Bitmap, Image, or Stream</param>
        /// <param name="settings">Will ignore ICC profile if ?ignoreicc=true.</param>
        /// <returns></returns>
        public virtual Bitmap LoadImage(object source, ResizeSettingsCollection settings) {

            //Fire PreLoadImage(source,settings)
            this.PreLoadImage(ref source, settings);

            System.Drawing.Bitmap b = null;

            string loadFailureReasons = "File may be corrupted, empty, or may contain a PNG image with a single dimension greater than 65,535 pixels.";

            bool useICM = true;
            if (settings != null && "true".Equals(settings["ignoreicc"], StringComparison.OrdinalIgnoreCase)) useICM = false;

            //Bitmap
            if (source is Bitmap) return source as Bitmap;
            //Image
            if (source is System.Drawing.Image) return new Bitmap((System.Drawing.Image)source);
            //IVirtualBitmapFile
            if (source is IVirtualBitmapFile) {
                b = ((IVirtualBitmapFile)source).GetBitmap();
                b.Tag = ((IVirtualBitmapFile)source).VirtualPath;
                return b;
            }
            
            //String, physical path
            if (source is string) {
                string path = source as string;
                try {
                    b = new System.Drawing.Bitmap(path, useICM);
                } catch (ArgumentException ae) {
                    ae.Data.Add("path", path);
                    ae.Data.Add("possiblereason",loadFailureReasons);
                    throw ae;
                } catch (ExternalException ee){
                    ee.Data.Add("path", path);
                    ee.Data.Add("possiblereason", loadFailureReasons);
                    throw ee;
                }
                if (b == null) throw new IOException("Could not read the specified image! Image invalid.");
                b.Tag = path;
                return b;
            }
            //VirtualFile
            //HttpPostedFile
            //Stream
            if (source is VirtualFile || source is HttpPostedFile || source is Stream) {
                Stream s = null;
                string path = null;
                if (source is Stream) s = (Stream)source;
                else if (source is HttpPostedFile) {
                    path = ((HttpPostedFile)source).FileName;
                    s = ((HttpPostedFile)source).InputStream;
                }
                if (source is VirtualFile) {
                    path = ((VirtualFile)source).VirtualPath;
                    s = ((VirtualFile)source).Open();
                }

                using (s) {
                    try {
                        b = new System.Drawing.Bitmap(s, useICM);
                    } catch (ArgumentException ae) {
                        ae.Data.Add("path", path);
                        ae.Data.Add("possiblereason", loadFailureReasons);
                        throw ae;
                    } catch (ExternalException ee) {
                        ee.Data.Add("path", path);
                        ee.Data.Add("possiblereason", loadFailureReasons);
                        throw ee;
                    }
                    if (b == null) throw new IOException("Could not read the specified image! Image invalid.");
                    b.Tag = path;
                    return b;
                }
            }
            throw new ArgumentException("Paramater source may only be an instance of string, VirtualFile, IVirtualBitmapFile, HttpPostedFile, Bitmap, Image, or Stream.", "source");
        }

        /// <summary>
        /// Builds a bitmap as if it were destined for a PNG (transparency is preserved)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public virtual Bitmap Build(object source, ResizeSettingsCollection settings) {
            BitmapHolder bh = new BitmapHolder();
            Build(source, bh, settings);
            return bh.bitmap;
        }

        /// <summary>
        /// All Build() calls funnel through here. 
        /// </summary>
        /// <param name="source">May be an instance of string (a physical path), VirtualFile, IVirtualBitmapFile, HttpPostedFile, Bitmap, Image, or Stream.</param>
        /// <param name="dest">May be a physical path (string), or a Stream instance</param>
        /// <param name="settings"></param>
        public virtual void Build(object source, object dest, ResizeSettingsCollection settings) {
            ResizeSettingsCollection s = new ResizeSettingsCollection(settings);

            Bitmap b = null; 
            try {
                
                //Load image
                b = LoadImage(source,settings);

                
                //Fire PreAcquireStream(ref dest, settings)
                this.PreAcquireStream(ref dest, settings);
                
                //Write to Physical file
                if (dest is string) {
                    System.IO.FileStream fs = new FileStream((string)dest, FileMode.Create, FileAccess.Write);
                    using (fs) {
                        buildToStream(b, fs, s);
                    }
                //Write to Unknown stream
                } else if (dest is Stream) {
                    buildToStream(b, (Stream)dest, s);
                //Write to BitmapHolder
                } else if (dest is BitmapHolder) {
                    ((BitmapHolder)dest).bitmap = buildToBitmap(b, s, true);
                    b = null; //So the b.Dispose doesn't happen
                } else throw new ArgumentException("Paramater dest may only be a string, Stream, or BitmapHolder", "dest");

            } finally {
                if (b != null) b.Dispose();
            }

        }
        /// <summary>
        /// Override this when you need to override the behavior of image encoding and/or Bitmap processing
        /// Not for external use.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <param name="settings"></param>
        protected virtual void buildToStream(Bitmap source, Stream dest, ResizeSettingsCollection settings) {
            ImageOutputSettings ios = new ImageOutputSettings(source, settings);
            using (Bitmap b = buildToBitmap(source, settings,ios.SupportsTransparency)) {//Determines output format, includes code for saving in a variety of formats.
                //Saves to stream
                ios.SaveImage(dest, b);
            }
        }
        /// <summary>
        /// Override this when you need to override the behavior of Bitmap processing. 
        /// Not for external use.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        protected virtual Bitmap buildToBitmap(Bitmap source, ResizeSettingsCollection settings, bool transparencySupported) {
            Bitmap b = null;
            using (ImageState state = new ImageState(settings, source.Size, Configuration.MaxSize,transparencySupported)) {
                state.sourceBitmap = source;

                //Generic processing of ImageState instances.
                Process(state);

                //Save a reference to return
                b = state.destBitmap;
                state.destBitmap = null; //So it doesn't get disposed yet
            }
            return b;
        }

        /// <summary>
        /// Processes an ImageState instance. Used by Build, GetFinalSize, and TranslatePoint. 
        /// </summary>
        /// <param name="s"></param>
        public virtual void Process(ImageState s){
            BeginProcess(s);
            PrepareSourceBitmap(s);  // We select the page/frame and flip the source bitmap here
            PostPrepareSourceBitmap(s);
            Layout(s);  // Layout all elements and determine how much room we need
            PrepareDestinationBitmap(s); //Create a bitmap and graphics object based on s.destSize
            Render(s); //Render using the graphics object
            RenderComplete(s);
            ProcessFinalBitmap(s); //Perform the final flipping of the bitmap.
            EndProcess(s);
        }
        /// <summary>
        /// Handles the layout phase of Processing
        /// </summary>
        /// <param name="s"></param>
        protected virtual void Layout(ImageState s){
            BeginLayout(s);
            FlipExistingPoints(s);
            LayoutImage(s);
            PostLayoutImage(s);
            LayoutPadding(s);
            PostLayoutPadding(s);
            LayoutBorder(s);
            PostLayoutBorder(s);
            LayoutEffects(s);
            PostLayoutEffects(s);
            LayoutMargin(s);
            PostLayoutMargin(s);
            LayoutRotate(s);
            PostLayoutRotate(s);
            LayoutNormalize(s);
            PostLayoutNormalize(s);
            LayoutRound(s);
            PostLayoutRound(s);
            EndLayout(s);
        }
        /// <summary>
        /// Handles the rendering phase of processing
        /// </summary>
        /// <param name="s"></param>
        protected virtual void Render(ImageState s){
            BeginRender(s);
            RenderBackground(s);
            PostRenderBackground(s);
            RenderEffects(s);
            PostRenderEffects(s);
            RenderPadding(s);
            PostRenderPadding(s);
            RenderImage(s);
            PostRenderImage(s);
            RenderBorder(s);
            PostRenderBorder(s);
            RenderOverlays(s);
            PostRenderOverlays(s);
            EndRender(s);
        }


        protected override void  PrepareSourceBitmap(ImageState s)
        {
 	        base.PrepareSourceBitmap(s); //Call extensions

            if (s.sourceBitmap == null) return; //Nothing to do if there is no bitmap

            Bitmap src = s.sourceBitmap;
            ResizeSettingsCollection q = s.settings;

            int page = 0;
            if (!string.IsNullOrEmpty(q["page"]) && !int.TryParse(q["page"], out page))
                page = 0;

            int frame = 0;
            if (!string.IsNullOrEmpty(q["frame"]) && !int.TryParse(q["frame"], out frame))
                frame = 0;

            //So users can use 1-based numbers
            page--; frame--;

            //Support page selection in a .tiff document.
            try {
                //Stay on the last page/frame if out of bounds
                if (page > 0 && page >= src.GetFrameCount(FrameDimension.Page)) page = src.GetFrameCount(FrameDimension.Page) - 1;
                if (frame > 0 && frame >= src.GetFrameCount(FrameDimension.Time)) frame = src.GetFrameCount(FrameDimension.Time) - 1;

                //Select the right page/frame if specified
                if (page > 0) src.SelectActiveFrame(FrameDimension.Page, page);
                if (frame > 0) src.SelectActiveFrame(FrameDimension.Time, frame);
            } catch (ExternalException) { } //When somebody tries &frame or &page on a single-frame image
            


            //Flipping has to be done on the original - it can't be done as part of the DrawImage or later, after the borders are drawn.

            if (s.sourceBitmap != null && s.settings.SourceFlip != RotateFlipType.RotateNoneFlipNone) {
                s.sourceBitmap.RotateFlip(s.settings.SourceFlip);
                s.originalSize = s.sourceBitmap.Size;
            }
            
        }


        override void LayoutPadding(ImageState s) {
            base.LayoutPadding(s);

            //We need to add padding
            if (!s.settings.Padding.IsEmpty) {
                s.layout.AddRing("padding",  s.settings.Padding);
            }
        }

        override void LayoutBorder(ImageState s) {
            base.LayoutBorder(s);

            //And borders
            if (!s.settings.Border.IsEmpty) {
                s.layout.AddRing("border", s.settings.Border);
            }
        }

        protected override void  LayoutEffects(ImageState s)
        {
 	         base.LayoutEffects(s);
            //Clone last ring, then offset it.
            if (s.settings["shadowWidth"] != null) {
                float shadowWidth = Utils.getFloat(s.settings,"shadowWidth",0);


                PointF shadowOffset = Utils.parsePointF(s.settings["shadowOffset"]);

                //For drawing purposes later
                s.layout.AddInvisiblePolygon("shadowInner", PolygonMath.MovePoly(s.layout.LastRing.points,shadowOffset));

                //For layout purposes
                s.layout.AddRing("shadow",PolygonMath.InflatePoly(s.layout.LastRing.points, new float[]{
                    Math.Max(0, shadowWidth - shadowOffset.Y),
                    Math.Max(0, shadowWidth + shadowOffset.X),
                    Math.Max(0, shadowWidth + shadowOffset.Y),
                    Math.Max(0, shadowWidth - shadowOffset.X)
                }));
            }
        }

        protected override void  LayoutRound(ImageState s)
        {
 	        base.LayoutRound(s);
            //Todo, round points here.
            //s.layout.Round();
        }
        
        protected override void  LayoutRotate(ImageState s)
        {
 	         base.LayoutRotate(s);
            //Now, rotate all rings.
            s.layout.Rotate(s.settings.Rotate, new PointF(0, 0));

        }
        protected override void  LayoutNormalize(ImageState s)
        {
 	        base.LayoutNormalize(s);
            //Normalize all the rings
            s.layout.Normalize(new PointF(0, 0));
        }
        

        protected override void  EndLayout(ImageState s)
        {
 	         base.EndLayout(s);

            //Calculates a bounding box around all the rings in the layout, then rounds that size. Creates a 1x1 pixel destSize value at minimum.
            s.destSize = PolygonMath.RoundPoints(s.layout.GetBoundingBox().Size);
            s.destSize = new Size((int)Math.Max(1, s.destSize.Width), (int)Math.Max(1, s.destSize.Height));
        }

        /// <summary>
        /// Creates a bitmap of s.destSize dimensions, intializes a graphics object for it, and configures all the default settings.
        /// </summary>
        /// <param name="s"></param>
        protected override void  PrepareDestinationBitmap(ImageState s)
        {
 	         base.PrepareDestinationBitmap(s);

             if (s.sourceBitmap == null) return;

            //Create new bitmap using calculated size. 
            s.destBitmap = new Bitmap(s.destSize.Width,s.destSize.Height, PixelFormat.Format32bppArgb);
            

            //Create graphics handle
            Graphics g = s.destGraphics = Graphics.FromImage(s.destBitmap);

            //High quality everthing
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.CompositingMode = CompositingMode.SourceOver;
        }


        protected override void RenderBackground(ImageState s) {
            base.RenderBackground(s);
            Graphics g = s.destGraphics;

            //If the image doesn't support transparency, we need to fill the background color now.
            Color background = s.settings.BackgroundColor;

            if (background == Color.Transparent)
                //Only set the bgcolor if the image isn't taking the whole area.
                if (!s.supportsTransparency && !PolygonMath.GetBoundingBox(s.layout["image"]).Equals(s.layout.GetBoundingBox()))
                    background = Color.White;
            
            //Fill background
            if (background != Color.Transparent) //This causes increased aliasing at the edges - i.e., a faint white border that is even more pronounced than usual.
                g.Clear(background); //Does this work for Color.Transparent? -- 

        }

        protected override void RenderEffects(ImageState s) {
            base.RenderEffects(s);


            //parse shadow
            Color shadowColor = Utils.parseColor(s.settings["shadowColor"], Color.Transparent);
            int shadowWidth = Utils.getInt(s.settings, "shadowWidth", -1);

            //Skip on transparent or 0-width shadow
            if (shadowColor == Color.Transparent || shadowWidth <= 0) return; 

            //Offsets may show inside the shadow - so we have to fix that
            s.destGraphics.FillPolygon(new SolidBrush(shadowColor),
                PolygonMath.InflatePoly(s.layout["shadowInner"], 1)); //Inflate 1 for FillPolgyon rounding errors.

            //Then we can draw the outer gradient
            Utils.DrawOuterGradient(s.destGraphics, s.layout["shadowInner"],
                             shadowColor, Color.Transparent, shadowWidth);


        }

        protected override void RenderPadding(ImageState s) {
            base.RenderPadding(s);

            Color paddingColor = s.settings.PaddingColor;
            //Inherit color
            if (paddingColor.Equals(Color.Transparent)) paddingColor = s.settings.BackgroundColor;
            //Draw padding around image if needed.
            if (!paddingColor.Equals(s.settings.BackgroundColor) && paddingColor != Color.Transparent)
                s.destGraphics.FillPolygon(new SolidBrush(paddingColor), s.layout["padding"]);


        }

        protected override void CreateImageAttribues(ImageState s) {
            base.CreateImageAttribues(s);
            if (s.copyAttibutes == null) s.copyAttibutes = new ImageAttributes();
        }

        protected override void RenderImage(ImageState s) {
            base.RenderImage(s);

            s.copyAttibutes.SetWrapMode(WrapMode.TileFlipXY);
            s.destGraphics.DrawImage(s.sourceBitmap, PolygonMath.getParallelogram(s.layout["image"]), s.copyRect, GraphicsUnit.Pixel, s.copyAttibutes);

        }

        protected override void RenderBorder(ImageState s) {
            base.RenderBorder(s);

            //Draw border
            if (s.settings.Border.IsEmpty) return;

            if (s.settings.Border.All <= 0) throw new NotImplementedException("Separate border widths have not yet been implemented");

            Pen p = new Pen(s.settings.BorderColor, (float)s.settings.Border.All);
            p.Alignment = System.Drawing.Drawing2D.PenAlignment.Center; //PenAlignment.Center is the only supported mode.
            p.LineJoin = System.Drawing.Drawing2D.LineJoin.Miter;
            s.destGraphics.DrawPolygon(p, PolygonMath.InflatePoly(s.layout["border"], (float)(s.settings.Border.All / -2.0))); //I hope GDI rounds the same way as .NET.. Otherwise there may be an off-by-one error..

        }


        protected override void EndRender(ImageState s) {
            base.EndRender(s);

            if (s.destGraphics == null) return;
            //Commit changes.
            s.destGraphics.Flush(FlushIntention.Flush);
            s.destGraphics.Dispose();
            s.destGraphics = null;
        }

        protected override void ProcessFinalBitmap(ImageState s) {
            base.ProcessFinalBitmap(s);

            //The last flipping.
            if (s.settings.Flip != RotateFlipType.RotateNoneFlipNone)
                s.destBitmap.RotateFlip(s.settings.Flip);

        }
       

        /// <summary>
        /// Doesn't support rotation or flipping. Translate a point on the original bitmap to a point on the new bitmap. If the original point no longer exists, returns Empty
        /// </summary>
        /// <returns></returns>
         public virtual PointF TranslatePoint(PointF sourcePoint, SizeF originalSize, NameValueCollection q)
         {
            
         }
         


        /// <summary>
        /// Gets the final size of an image
        /// </summary>
        /// <returns></returns>
        public virtual Size GetFinalSize(SizeF originalSize, NameValueCollection q)
        {
        }

  




       /// <summary>
       /// Populates copyRect, as well as Rings image and imageArea
       /// </summary>
       /// <param name="s"></param>
        protected override void LayoutImage(ImageState s) {
            //Use the crop size if present.
            s.copyRect = new RectangleF(new PointF(0,0),s.originalSize);
            if (s.settings.CropMode == CropMode.Custom) {
                s.copyRect = s.settings.getCustomCropSourceRect(s.originalSize);
                if (s.copyRect.Size.IsEmpty) throw new Exception("You must specify a custom crop rectange if crop=custom");
            }

            //Aspect ratio of the image
            double imageRatio = s.copySize.Width / s.copySize.Height;

            //Was any dimension specified?
            bool dimensionSpecified = s.settings.Width != -1 || s.settings.Height != -1 || s.settings.MaxHeight != -1 || s.settings.MaxWidth != -1;

            //The target size for the image 
            SizeF targetSize = new SizeF(-1, -1);
            SizeF areaSize = new SizeF(-1, -1);
            if (!dimensionSpecified) {
                areaSize = targetSize = s.copySize; //No dimension - use original size if possible - within web.config bounds.
                //Checks against the web.config bounds ('finalSizeBounds')
                if (!PolygonMath.FitsInside(targetSize, s.maxSize)) {
                    //Scale down to fit. Doesn't matter what the scale setting is... No dimensions were specified.
                    areaSize = targetSize = PolygonMath.DownScaleInside(targetSize, s.maxSize);
                }

            } else {
                //A dimension was specified. 
                //We first calculate the largest size the image can be under the width/height/maxwidth/maxheight restrictions.
                //- pretending stretch=fill and scale=both

                //Temp vars - results stored in targetSize and areaSize
                double width = s.settings.Width;
                double height = s.settings.Height;
                double maxwidth = s.settings.MaxWidth;
                double maxheight = s.settings.MaxHeight;

                //Eliminate cases where both a value and a max value are specified: use the smaller value for the width/height 
                if (maxwidth > 0 && width > 0) {
                    width = Math.Min(maxwidth, width);
                    maxwidth = -1;
                }
                if (maxheight > 0 && height > 0) {
                    height = Math.Min(maxheight, height);
                    maxheight = -1;
                }
                //Do sizing logic

                if (width > 0 || height > 0) //In this case, either (or both) width and height were specified 
                {
                    //If only one is specified, calculate the other from 
                    if (width > 0) {
                        if (height < 0) height = (width / imageRatio);
                        if (maxheight > 0 && height > maxheight) height = maxheight; //Crop to maxheight value

                    } else if (height > 0) {
                        if (width < 0) width = (height * imageRatio);
                        if (maxheight > 0 && height > maxheight) height = maxheight; //Crop to maxheight value
                    }
                    //Store result
                    targetSize = new SizeF((float)width, (float)height);
                } else //In this case, only maxwidth and/or maxheight were specified.
                {
                    //Calculate the missing max bounds (if one *is* missing), using aspect ratio of the image
                    if (maxheight > 0 && maxwidth <= 0)
                        maxwidth = maxheight * imageRatio;
                    else if (maxwidth > 0 && maxheight <= 0)
                        maxheight = maxwidth / imageRatio;

                    //Scale image coords to fit.
                    targetSize = PolygonMath.ScaleInside(s.copySize, new SizeF((float)maxwidth, (float)maxheight));

                }

                //We now have targetSize. targetSize will only be a different aspect ratio if both 'width' and 'height' are specified.

                //Checks against the web.config bounds ('finalSizeBounds')
                if (!PolygonMath.FitsInside(targetSize, s.maxSize)) {
                    //Scale down to fit using existing aspect ratio.
                    targetSize = PolygonMath.ScaleInside(targetSize, s.maxSize);
                }
                //This will be the area size also
                areaSize = targetSize;

                //Now do scale=proportionally check. Set targetSize=imageSize and make it fit within areaSize using ScaleInside.
                if (s.settings.Stretch == StretchMode.Proportionally) {
                    targetSize = PolygonMath.ScaleInside(s.copySize, areaSize);
                }

                //Now do upscale/downscale checks. If they take effect, set targetSize to imageSize
                if (s.settings.Scale == ScaleMode.DownscaleOnly) {
                    if (PolygonMath.FitsInside(s.copySize, targetSize)) {
                        //The image is smaller or equal to its target polygon. Use original image coordinates instead.

                        if (!PolygonMath.FitsInside(s.copySize, s.maxSize)) //Check web.config 'finalSizeBounds' and scale to fit.
                            areaSize = targetSize = PolygonMath.ScaleInside(s.copySize, s.maxSize);
                        else
                            areaSize = targetSize = s.copySize;
                    }
                } else if (s.settings.Scale == ScaleMode.UpscaleOnly) {
                    if (!PolygonMath.FitsInside(s.copySize, targetSize)) {
                        //The image is larger than its target. Use original image coordintes instead
                        if (!PolygonMath.FitsInside(s.copySize, s.maxSize)) //Check web.config 'finalSizeBounds' and scale to fit.
                            areaSize = targetSize = PolygonMath.ScaleInside(s.copySize, s.maxSize);
                        else
                            areaSize = targetSize = s.copySize;

                    }
                } else if (s.settings.Scale == ScaleMode.UpscaleCanvas) {
                    //Same as downscaleonly, except areaSize isn't changed.
                    if (PolygonMath.FitsInside(s.copySize, targetSize)) {
                        //The image is smaller or equal to its target polygon. Use original image coordinates instead.

                        if (!PolygonMath.FitsInside(s.copySize, s.maxSize)) //Check web.config 'finalSizeBounds' and scale to fit.
                            targetSize = PolygonMath.ScaleInside(s.copySize, s.maxSize);
                        else
                            targetSize = s.copySize;
                    }

                }

            }

            //June 3: Ensure no dimension of targetSize or areaSize is less than 1px;
            areaSize.Width = Math.Max(1, areaSize.Width);
            areaSize.Height = Math.Max(1, areaSize.Height);
            targetSize.Width = Math.Max(1, targetSize.Width);
            targetSize.Height = Math.Max(1, targetSize.Height);
            
            
            
            //Autocrop
            if (s.settings.CropMode == CropMode.Auto && s.settings.Stretch == StretchMode.Proportionally) {
                //Determine the size of the area we are copying
                SizeF sourceSize = PolygonMath.ScaleInside(areaSize, s.originalSize);
                //Center the portion we are copying within the original bitmap
                s.copyRect = new RectangleF((s.originalSize.Width - sourceSize.Width) / 2, (s.originalSize.Height - sourceSize.Height) / 2, sourceSize.Width, sourceSize.Height);
                //Restore targetSize to match areaSize //Warning - crop always forces scale=both.
                targetSize = areaSize;
            }

            s.layout.AddRing("image", PolygonMath.ToPoly(new RectangleF(new PointF(0, 0), targetSize)));

            s.layout.AddRing("imageArea",PolygonMath.ToPoly(new RectangleF(new PointF(0, 0), areaSize)));

            //Center imageArea around 'image'
            s.layout["imageArea"] = PolygonMath.CenterInside(s.layout["imageArea"], s.layout["image"]);

        }



        #region Legacy BuildImage overloads

                public virtual Bitmap BuildImage(Bitmap src, ImageFormat originalFormat, NameValueCollection q)
        {
            ResizeSettingsCollection c = new ResizeSettingsCollection(q);
            c.SetDefaultImageFormat(ImageOutputSettings.GetExtensionFromImageFormat(originalFormat));
            return Build(src, c);
        }

        /// <summary>
        /// Generates a resized bitmap from the specifed source file and the specified querystring. Understands width/height and maxwidth/maxheight.
        /// Throws either an ArgumentException or IOException if the source image is invalid.
        /// Always use ImageOutputSettings to save images, since Image.Save doesn't work well for GIF or PNGs, and needs custom params for Jpegs.
        /// </summary>
        /// 
        /// <returns></returns>
        public virtual Bitmap BuildImage(string sourceFile, NameValueCollection queryString) {
            return Build(sourceFile, new ResizeSettingsCollection(queryString));
        }
        public virtual Bitmap BuildImage(HttpPostedFile sourceFile, NameValueCollection queryString) {
            return Build(sourceFile, new ResizeSettingsCollection(queryString));
        }
        public virtual Bitmap BuildImage(VirtualFile sourceFile, NameValueCollection queryString) {
            return Build(sourceFile, new ResizeSettingsCollection(queryString));
        }
        public virtual void BuildImage(string sourceFile, string targetFile, NameValueCollection queryString) {
            Build(sourceFile, targetFile, new ResizeSettingsCollection(queryString));
        }
        public virtual void BuildImage(VirtualFile sourceFile, string targetFile, NameValueCollection queryString) {
            Build(sourceFile, targetFile, new ResizeSettingsCollection(queryString));
        }
        public virtual void BuildImage(IVirtualBitmapFile sourceFile, string targetFile, NameValueCollection queryString) {
            Build(sourceFile, targetFile, new ResizeSettingsCollection(queryString));
        }
        public virtual void BuildImage(HttpPostedFile sourceFile, string targetFile, NameValueCollection queryString) {
            Build(sourceFile, targetFile, new ResizeSettingsCollection(queryString));
        }
      
        #endregion
    }
}
