﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt */
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageResizer.Plugins {
    public interface IVirtualBitmapFile {
        System.Drawing.Bitmap GetBitmap();
        string VirtualPath { get; }
    }
}
