﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;

namespace ImageResizer.Configuration {
    public interface IUrlEventArgs {
        NameValueCollection QueryString { get; set; }
        string VirtualPath { get; set; }
    }
}
