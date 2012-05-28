﻿using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Configuration;
using System.Web.Hosting;
using System.Collections.Specialized;

namespace ImageResizer.Plugins.Basic {
    /// <summary>
    /// Adds URL syntax support for http://webimageresizer.codeplex.com/, 
    /// http://imagehandler.codeplex.com/, http://bbimagehandler.codeplex.com/, http://dynamicimageprocess.codeplex.com/, 
    /// and http://bip.codeplex.com/
    /// </summary>
    public class ImageHandlerSyntax:IPlugin {

        Config c; 

        public IPlugin Install(Config c) {
            this.c = c;
            c.Plugins.add_plugin(this);
            c.Pipeline.PostAuthorizeRequestStart += Pipeline_PostAuthorizeRequestStart;
            return this;
        }

        void Pipeline_PostAuthorizeRequestStart(System.Web.IHttpModule sender, System.Web.HttpContext context) {
            string prefix = HostingEnvironment.ApplicationVirtualPath.TrimEnd('/') + '/';

            if (c.Pipeline.PreRewritePath.Equals(prefix + "ImageHandler.ashx", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(context.Request.QueryString["src"])) {
                //WebImageResizer: http://webimageresizer.codeplex.com/

                /*src: Key to an image, can be a relative url, guid or whatever you want
                width: Width in pixels without any unit specification
                height: Height in pixels without any unit specification
                format: Formats to output to one of the image formats: png, gif, jpg, tif
                greyscale: any non empty value triggers grey scaling
                rotate: Invariant Culture floating point number in degrees where 360 is a complete rotation.
                zoom: Invariant Culture floating point number indicating scale value. If 2 is specified the output will be double the original size.*/

                //Fix path
                c.Pipeline.PreRewritePath = prefix + c.Pipeline.ModifiedQueryString["src"];
                c.Pipeline.ModifiedQueryString.Remove("src");

                //Grayscale and zoom not supported yet
                if (string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["s.grayscale"]) &&
                    !string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["greyscale"])) c.Pipeline.ModifiedQueryString["s.grayscale"] = "true";

                if (string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["s.invert"]) &&
                    !string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["invert"])) c.Pipeline.ModifiedQueryString["s.invert"] = "true";


                //Mimic aspect-ratio destruction
                if (string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["stretch"]) && string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["mode"]))
                    c.Pipeline.ModifiedQueryString["mode"] = "stretch";

            }else if (c.Pipeline.PreRewritePath.Equals(prefix + "imghandler.ashx", StringComparison.OrdinalIgnoreCase) && 
                !string.IsNullOrEmpty(context.Request.QueryString["img"])) {
                //Image handler for ASP.NET 2.0: http://www.yoursite.com/imghandler.ashx?h=100&w=100&img=yourfolder/yourimage.jpg 

                //Fix path
                c.Pipeline.PreRewritePath = prefix + c.Pipeline.ModifiedQueryString["img"];
                //Edit querystring
                c.Pipeline.ModifiedQueryString["width"] = c.Pipeline.ModifiedQueryString["w"];
                c.Pipeline.ModifiedQueryString["height"] = c.Pipeline.ModifiedQueryString["h"];
                c.Pipeline.ModifiedQueryString.Remove("w");
                c.Pipeline.ModifiedQueryString.Remove("h");
                c.Pipeline.ModifiedQueryString.Remove("img");

                //Mimic aspect-ratio destruction
                if (string.IsNullOrEmpty(c.Pipeline.ModifiedQueryString["stretch"]))
                    c.Pipeline.ModifiedQueryString["stretch"] = "fill";
            } else if (c.Pipeline.PreRewritePath.Equals(prefix + "bbimagehandler.ashx", StringComparison.OrdinalIgnoreCase) &&
                 !string.IsNullOrEmpty(context.Request.QueryString["file"])) {
                //Only file requests for bbimagehandler are supported. SQL and website thumbnails are not.
                //bbimagehandler.ashx?File=Winter.jpg&width=150&ResizeMode=FitSquare&BackColor=#F58719&border=10"
                /*width: width in pixel of resulting image
                height: height in pixel of resulting image
                resizemode:
                fit: Fit mode maintains the aspect ratio of the original image while ensuring that the dimensions of the result do not exceed the maximum values for the resize transformation. (Needs width or height parameter)
                fitsquare: Resizes the image with the given width as its longest side (depending on image direction) and maintains the aspect ratio. The image will be centered in a square area of the chosen background color (Needs width parameter, backcolor optional)
                crop: Crop resizes the image and removes parts of it to ensure that the dimensions of the result are exactly as specified by the transformation.(Needs width and height parameter)
                backcolor: color of background or/and  border when resizemode is fitsquare or fit.
                border: border width in pixels around the image (added to width / height) when resizemode is fitsquare or fit.
                format: jpg,png,bmp or gif, defines the format of the resulting image
                 */

                NameValueCollection q = c.Pipeline.ModifiedQueryString;
                //Fix path
                c.Pipeline.PreRewritePath = prefix + q["file"];
                q.Remove("file");

                if ("fit".Equals(q["resizemode"])) {
                    q["maxwidth"] = q["width"];
                    q["maxheight"] = q["height"];
                    q.Remove("width");
                    q.Remove("height");
                    q.Remove("resizemode");
                } else if ("fitsquare".Equals(q["resizemode"])) {
                    q["height"] = q["width"]; //Copy width, make it a square
                } else if ("crop".Equals(q["resizemode"])) {
                    q["crop"] = "auto";
                }
                if (!string.IsNullOrEmpty(q["backcolor"])) {
                    q["bgcolor"] = q["backcolor"];
                    q.Remove("backcolor");
                }

                if (!string.IsNullOrEmpty(q["border"])) {
                    q["borderWidth"] = q["border"];
                    q.Remove("border");
                }
            } 
            //Feb. 20 - removed this, as the w/h syntax has already been adopted by the image resizer, and this code was breaking URLs when both w and height or h and width were used togehter.
            //else if (c.Pipeline.IsAcceptedImageType(c.Pipeline.PreRewritePath) || c.Pipeline.SkipFileTypeCheck){
            //    //BetterImageProcessor and DynamicImageProcessor uses a Handler registered to all Jpeg images. Just the image URL plus ?w= and/or ?h=
            //    if (!string.IsNullOrEmpty(context.Request.QueryString["w"]) || !string.IsNullOrEmpty(context.Request.QueryString["h"])) {
            //        c.Pipeline.ModifiedQueryString["width"] = c.Pipeline.ModifiedQueryString["w"];
            //        c.Pipeline.ModifiedQueryString["height"] = c.Pipeline.ModifiedQueryString["h"];
            //        c.Pipeline.ModifiedQueryString.Remove("w");
            //        c.Pipeline.ModifiedQueryString.Remove("h");
            //        //Note - the module will not lose aspect ratio even though BIP does. Although implemented for other syntaxes, 
            //        // this syntax (w/h) may eventually be adopted by the image resizer, so we don't want issues down the road.
            //    }
                

            //}
        }

       
        

        public bool Uninstall(Configuration.Config c) {
            c.Plugins.remove_plugin(this);
            c.Pipeline.PostAuthorizeRequestStart -= Pipeline_PostAuthorizeRequestStart;
            return true;
        }

    }
}
