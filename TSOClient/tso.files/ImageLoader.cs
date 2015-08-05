﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FSO.Files
{
    public class ImageLoader
    {
        public static uint[] MASK_COLORS = new uint[]{
            new Microsoft.Xna.Framework.Color(0xFF, 0x00, 0xFF, 0xFF).PackedValue,
            new Microsoft.Xna.Framework.Color(0xFE, 0x02, 0xFE, 0xFF).PackedValue,
            new Microsoft.Xna.Framework.Color(0xFF, 0x01, 0xFF, 0xFF).PackedValue
        };

        public static Texture2D FromStream(GraphicsDevice gd, Stream str)
        {
            try
            {
                return Texture2D.FromStream(gd, str);
            }
            catch (Exception)
            {
                try
                {
                    bool premultiplied = false;
                    Bitmap bmp = null;
                    try {
                        bmp = (Bitmap)Image.FromStream(str); //try as bmp
                    } catch (Exception) {
                        str.Seek(0, SeekOrigin.Begin);
                        var tga = new Paloma.TargaImage(str);
                        bmp = tga.Image; //try as tga. for some reason this format does not have a magic number, which is ridiculously stupid.
                    }

                    var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var bytes = new byte[data.Height * data.Stride];

                    // copy the bytes from bitmap to array
                    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                    for (int i = 0; i < bytes.Length; i += 4)
                    { //flip red and blue and premultiply alpha
                        byte temp = bytes[i + 2];
                        float a = (premultiplied) ? 1 : (bytes[i + 3] / 255f);
                        bytes[i + 2] = (byte)(bytes[i] * a);
                        bytes[i + 1] = (byte)(bytes[i + 1] * a);
                        bytes[i] = (byte)(temp * a);
                    }

                    var tex = new Texture2D(gd, data.Width, data.Height);
                    tex.SetData<byte>(bytes);
                    return tex;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
