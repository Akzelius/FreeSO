﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using FSO.Files.Utils;

namespace FSO.Files.Formats.IFF.Chunks
{
    /// <summary>
    /// This chunk type defines a list of interactions for an object and assigns a BHAV subroutine 
    /// for each interaction. The pie menu labels shown to the user are stored in a TTAs chunk with 
    /// the same ID.
    /// </summary>
    public class TTAB : IffChunk
    {
        public TTABInteraction[] Interactions = new TTABInteraction[0];
        public Dictionary<uint, TTABInteraction> InteractionByIndex = new Dictionary<uint, TTABInteraction>();

        /// <summary>
        /// Reads a TTAB chunk from a stream.
        /// </summary>
        /// <param name="iff">An Iff instance.</param>
        /// <param name="stream">A Stream object holding a TTAB chunk.</param>
        public override void Read(IffFile iff, Stream stream)
        {
            using (var io = IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                InteractionByIndex.Clear();
                Interactions = new TTABInteraction[io.ReadUInt16()];
                if (Interactions.Length == 0) return; //no interactions, don't bother reading remainder.
                var version = io.ReadUInt16();
                IOProxy iop;
                if (version != 9 && version != 10) iop = new TTABNormal(io);
                else
                {
                    var compressionCode = io.ReadByte();
                    if (compressionCode != 1) throw new Exception("hey what!!");
                    iop = new TTABFieldEncode(io); //haven't guaranteed that this works, since none of the objects in the test lot use it.
                }
                for (int i = 0; i < Interactions.Length; i++)
                {
                    var result = new TTABInteraction();
                    result.ActionFunction = iop.ReadUInt16();
                    result.TestFunction = iop.ReadUInt16();
                    result.MotiveEntries = new TTABMotiveEntry[iop.ReadUInt32()];
                    result.Flags = iop.ReadUInt32();
                    result.TTAIndex = iop.ReadUInt32();
                    if (version > 6) result.AttenuationCode = iop.ReadUInt32();
                    result.AttenuationValue = iop.ReadFloat();
                    result.AutonomyThreshold = iop.ReadUInt32();
                    result.JoiningIndex = iop.ReadInt32();
                    for (int j = 0; j < result.MotiveEntries.Length; j++)
                    {
                        var motive = new TTABMotiveEntry();
                        if (version > 6) motive.EffectRangeMinimum = iop.ReadInt16();
                        motive.EffectRangeMaximum = iop.ReadInt16();
                        if (version > 6) motive.PersonalityModifier = iop.ReadUInt16();
                        result.MotiveEntries[j] = motive;
                    }
                    if (version > 9) result.Unknown = iop.ReadUInt32();
                    Interactions[i] = result;
                    InteractionByIndex.Add(result.TTAIndex, result);
                }
            }
        }

        public override bool Write(IffFile iff, Stream stream)
        {
            using (var io = IoWriter.FromStream(stream, ByteOrder.LITTLE_ENDIAN))
            {
                io.WriteUInt16((ushort)Interactions.Length);
                io.WriteUInt16(8); //version. don't save to high version cause we can't write out using the complex io proxy.
                for (int i = 0; i < Interactions.Length; i++)
                {
                    var action = Interactions[i];
                    io.WriteUInt16(action.ActionFunction);
                    io.WriteUInt16(action.TestFunction);
                    io.WriteUInt32((uint)action.MotiveEntries.Length);
                    io.WriteUInt32(action.Flags);
                    io.WriteUInt32(action.TTAIndex);
                    io.WriteUInt32(action.AttenuationCode);
                    io.WriteFloat(action.AttenuationValue);
                    io.WriteUInt32(action.AutonomyThreshold);
                    io.WriteInt32(action.JoiningIndex);
                    for (int j=0; j < action.MotiveEntries.Length; j++)
                    {
                        var mot = action.MotiveEntries[j];
                        io.WriteInt16(mot.EffectRangeMinimum);
                        io.WriteInt16(mot.EffectRangeMaximum);
                        io.WriteUInt16(mot.PersonalityModifier);
                    }
                    //here is where we would write out unknown, if we cared about that.
                }
            }
            return true;
        }

        public void InsertInteraction(TTABInteraction action, int index)
        {
            var newInt = new TTABInteraction[Interactions.Length + 1];
            if (index == -1) index = 0;
            Array.Copy(Interactions, newInt, index); //copy before strings
            newInt[index] = action;
            Array.Copy(Interactions, index, newInt, index + 1, (Interactions.Length - index));
            Interactions = newInt;

            if (!InteractionByIndex.ContainsKey(action.TTAIndex)) InteractionByIndex.Add(action.TTAIndex, action);
        }

        public void DeleteInteraction(int index)
        {
            var action = Interactions[index];
            var newInt = new TTABInteraction[Interactions.Length - 1];
            if (index == -1) index = 0;
            Array.Copy(Interactions, newInt, index); //copy before strings
            Array.Copy(Interactions, index + 1, newInt, index, (Interactions.Length - (index + 1)));
            Interactions = newInt;

            if (InteractionByIndex.ContainsKey(action.TTAIndex)) InteractionByIndex.Remove(action.TTAIndex);
        }
    }

    abstract class IOProxy
    {
        public abstract ushort ReadUInt16();
        public abstract short ReadInt16();
        public abstract int ReadInt32();
        public abstract uint ReadUInt32();
        public abstract float ReadFloat();

        public IoBuffer io;
        public IOProxy(IoBuffer io)
        {
            this.io = io;
        }
    }

   class TTABNormal : IOProxy
    {
        public override ushort ReadUInt16() { return io.ReadUInt16(); }
        public override short ReadInt16() { return io.ReadInt16(); }
        public override int ReadInt32() { return io.ReadInt32(); }
        public override uint ReadUInt32() { return io.ReadUInt32(); }
        public override float ReadFloat() { return io.ReadFloat(); }

        public TTABNormal(IoBuffer io) : base(io) { }
    }

    /// <summary>
    /// Used to read values from field encoded stream.
    /// </summary>
    class TTABFieldEncode : IOProxy
    {
        private byte bitPos = 0;
        private byte curByte = 0;
        static byte[] widths = { 5, 8, 13, 16 };
        static byte[] widths2 = { 6, 11, 21, 32 };

        public void setBytePos(int n)
        {
            io.Seek(SeekOrigin.Begin, n);
            curByte = io.ReadByte();
            bitPos = 0;
        }

        public override ushort ReadUInt16() 
        {
            return (ushort)ReadField(false);
        }

        public override short ReadInt16()
        {
            return (short)ReadField(false);
        }

        public override int ReadInt32()
        {
            return (int)ReadField(true);
        }

        public override uint ReadUInt32()
        {
            return (uint)ReadField(true);
        }

        public override float ReadFloat()
        {
            return (float)ReadField(true);
            //this is incredibly wrong
        }

        private long ReadField(bool big)
        {
            if (ReadBit() == 0) return 0;

            uint code = ReadBits(2);
            byte width = (big)?widths2[code]:widths[code];
            long value = ReadBits(width);
            value |= -(value & (1 << (width-1)));

            return value;
        }

        private uint ReadBits(int n)
        {
            uint total = 0;
            for (int i = 0; i < n; i++)
            {
                total += (uint)(ReadBit() << ((n - i)-1));
            }
            return total;
        }

        private byte ReadBit()
        {
            byte result = (byte)((curByte & (1 << (7 - bitPos))) >> (7 - bitPos));
            if (++bitPos > 7)
            {
                bitPos = 0;
                try
                {
                    curByte = io.ReadByte();
                }
                catch (Exception)
                {
                    curByte = 0; //no more data, read 0
                }
            }
            return result;
        }

        public TTABFieldEncode(IoBuffer io) : base(io) 
        {
            curByte = io.ReadByte();
            bitPos = 0;
        }
    }

    /// <summary>
    /// Represents an interaction in a TTAB chunk.
    /// </summary>
    public class TTABInteraction
    {
        public ushort ActionFunction;
        public ushort TestFunction;
        public TTABMotiveEntry[] MotiveEntries;
        public uint Flags;
        public uint TTAIndex;
        public uint AttenuationCode;
        public float AttenuationValue;
        public uint AutonomyThreshold;
        public int JoiningIndex;
        public uint Unknown;

        public InteractionMaskFlags MaskFlags {
            get {
                return (InteractionMaskFlags)((Unknown >> 4) & 0xF);
            }
        }

        //ALLOW
        public bool AllowVisitors { get; set; }
        public bool AllowFriends { get; set; }
        public bool AllowRoommates { get; set; }
        public bool AllowObjectOwner { get; set; }
        public bool UnderParentalControl { get; set; }
        public bool AllowCSRs { get; set; }
        public bool AllowGhosts { get; set; }
        public bool AllowCats { get; set; }
        public bool AllowDogs { get; set; }

        //FLAGS
        public bool Debug
        {
            get { return ((TTABFlags)Flags & TTABFlags.Debug) > 0; }
            set { Flags &= ~((uint)TTABFlags.Debug); if (value) Flags |= (uint)TTABFlags.Debug; }
        }

        public bool Leapfrog {
            get { return ((TTABFlags)Flags & TTABFlags.Leapfrog) > 0; }
            set { Flags &= ~((uint)TTABFlags.Leapfrog); if (value) Flags |= (uint)TTABFlags.Leapfrog; }
        }
        public bool MustRun
        {
            get { return ((TTABFlags)Flags & TTABFlags.MustRun) > 0; }
            set { Flags &= ~((uint)TTABFlags.MustRun); if (value) Flags |= (uint)TTABFlags.MustRun; }
        }
        public bool AutoFirst { get; set; }
        public bool RunImmediately
        {
            get { return ((TTABFlags)Flags & TTABFlags.RunImmediately) > 0; }
            set { Flags &= ~((uint)TTABFlags.RunImmediately); if (value) Flags |= (uint)TTABFlags.RunImmediately; }
        }
        public bool AllowConsecutive { get; set; }


        public bool Carrying { get; set; }
        public bool Repair { get; set; }
        public bool AlwaysCheck { get; set; }
        public bool WhenDead { get; set; }
    }

    /// <summary>
    /// Represents a motive entry in a TTAB chunk.
    /// </summary>
    public struct TTABMotiveEntry
    {
        public short EffectRangeMinimum;
        public short EffectRangeMaximum;
        public ushort PersonalityModifier;
    }

    public enum TTABFlags
    {
        RunImmediately = 1<<2,
        Debug = 1<<7,
        Leapfrog = 1<<9,
        MustRun = 1<<10
    }

    public enum InteractionMaskFlags
    {
        AvailableWhenCarrying = 1,
        IsRepair = 1<<1,
        RunCheckAlways = 1 << 2,
        AvailableWhenDead = 1 << 3,
    }
}
