/*
* Copyright (c) 2006, Jonas Beckeman
* All rights reserved.
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of Jonas Beckeman nor the names of its contributors
*       may be used to endorse or promote products derived from this software
*       without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY JONAS BECKEMAN AND CONTRIBUTORS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL JONAS BECKEMAN AND CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
* HEADER_END*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Endogine.Serialization;
namespace Endogine.Codecs.Photoshop
{
    public class BinaryPSDWriter : BinaryReverseWriter
    {
        public BinaryPSDWriter(Stream stream)
            : base(stream)
        { }


        public void WritePSDRectangle(Endogine.ERectangle rct)
        {
            this.Write((int)rct.Y);
            this.Write((int)rct.X);
            this.Write((int)rct.Bottom);
            this.Write((int)rct.Right);
        }
        public unsafe void WritePSDDouble(double value)
        {
            //TODO: examine PSD format!
            BinaryReverseReader.SwapBytes((byte*)&value, 2);
            base.Write(value);
        }
        public void WritePSDUnicode(string value)
        {
            //TODO: examine PSD format!
            base.Write((uint)value.Length);
            foreach (char c in value)
            {
                base.Write((byte)0);
                base.Write(c);
            }
        }
    }
}
