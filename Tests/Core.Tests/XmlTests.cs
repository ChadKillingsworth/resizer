﻿using System;
using System.Collections.Generic;
using System.Text;
using Gallio.Framework;
using MbUnit.Framework;
using MbUnit.Framework.ContractVerifiers;
using ImageResizer.Configuration;
using ImageResizer.Plugins.Basic;
using ImageResizer.Plugins;
using ImageResizer.Plugins.PluginB;
using SampleNamespace;
using ImageResizer.Configuration.Issues;
using System.Diagnostics;
using ImageResizer.Plugins.PluginC;
using ImageResizer.Encoding;
using ImageResizer.Caching;
using ImageResizer.Resizing;
namespace ImageResizer.Configuration.Xml {
    [TestFixture]
    public class XmlTests {

        [Test]
        [Row("<resizEr><DiskCACHE aTTr='valUE' /></resizEr>", "diskCache.attr", "valUE")] //Verify case-insensitivity
        public void TestCachedQueryAttr(string xml, string selector, string expectedValue) {
            IssueSink s = new IssueSink("XmlTests");
            Node n = Node.FromXmlFragment(xml, s); //Node, all start and end tags must match in case. XML rules.
            Assert.AreEqual(expectedValue, n.queryAttr(selector));
            IEnumerable<IIssue> issues = s.GetIssues();
            if (issues != null) foreach (IIssue issue in issues)
                    Debug.Write(issue.ToString());
        }


    }
}
