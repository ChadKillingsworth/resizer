﻿/* Copyright (c) 2011 Nathanael Jones. See license.txt */
using System;
using System.Collections.Generic;
using System.Text;
using ImageResizer.Configuration;

namespace ImageResizer.Plugins {
    public interface IPlugin {
        /// <summary>
        /// Installs the plugin in the specified Config instance. The plugin must handle all the work of loading settings, registering the plugin etc.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        IPlugin Install(Config c);
        /// <summary>
        /// Uninstalls the plugin. Should reverse all changes made during Install
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        bool Uninstall(Config c);
    }
}
