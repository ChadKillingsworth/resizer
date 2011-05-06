﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt */
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageResizer.Plugins {
    /// <summary>
    /// For virtual files who want to provide their data in Bitmap form (like a PSD reader or gradient generator)
    /// </summary>
    public interface IVirtualBitmapFile:IVirtualFile {
        /// <summary>
        /// Returns a Bitmap instance of the file's contents
        /// </summary>
        /// <returns></returns>
        System.Drawing.Bitmap GetBitmap();
    }
}
