﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FSO.SimAntics.Engine;
using FSO.Files.Utils;
using FSO.SimAntics.Model;

namespace FSO.SimAntics.Primitives
{
    public class VMInvokePlugin : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context)
        {
            var operand = context.GetCurrentOperand<VMInvokePluginOperand>();
            return VMPrimitiveExitCode.GOTO_TRUE;
        }
    }

    public class VMInvokePluginOperand : VMPrimitiveOperand 
    {
        public byte[] unknown;
        //byte 1: location of person id
        //byte 2: location of object id
        //byte 3: location of event target (var that event id goes into. value goes in temp 0)
        //byte 4: Joinable? (1/0)

        //bytes 5-8: unknown (id? it's like a guid or something)
        //sign: 160 86 99 42
        //pizzamakerplugin: 57 174 71 234

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                unknown = io.ReadBytes(8);
            }
        }
        #endregion
    }
}
