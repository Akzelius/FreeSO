﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FSO.LotView.Model;
using Microsoft.Xna.Framework;
using FSO.LotView.Components;
using FSO.SimAntics.Model;

namespace FSO.SimAntics.Entities
{
    /// <summary>
    /// Ties multiple entities together with a common name and set of repositioning functions.
    /// </summary>
    public class VMMultitileGroup
    {
        public bool MultiTile;
        public List<VMEntity> Objects = new List<VMEntity>();

        public VMEntity BaseObject
        {
            get
            {
                for (int i = 0; i < Objects.Count(); i++)
                {
                    var sub = Objects[i];
                    if (sub.Object.OBJ.MyLeadObject > 0) return sub;
                }

                for (int i = 0; i < Objects.Count(); i++)
                {
                    var sub = Objects[i];
                    if ((((ushort)sub.Object.OBJ.SubIndex) >> 8) == 0 && (((ushort)sub.Object.OBJ.SubIndex) & 0xFF) == 0 && sub.Object.OBJ.LevelOffset == 0) return sub;
                }
                return Objects[0];
            }
        }

        public Vector3[] GetBasePositions()
        {
            Vector3[] positions = new Vector3[Objects.Count];
            for (int i = 0; i < Objects.Count(); i++)
            {
                ushort sub = (ushort)Objects[i].Object.OBJ.SubIndex;
                positions[i] = new Vector3((sbyte)(sub >> 8), (sbyte)(sub & 0xFF), 0);
            }
            return positions;
        }

        public VMPlacementError ChangePosition(LotTilePos pos, Direction direction, VMContext context)
        {
            if (pos.Level > context.Architecture.Stories) return VMPlacementError.NotAllowedOnFloor;
            for (int i = 0; i < Objects.Count(); i++) Objects[i].PrePositionChange(context);

            int Dir = 0;
            switch (direction)
            {
                case Direction.NORTH:
                    Dir = 0; break;
                case Direction.EAST:
                    Dir = 2; break;
                case Direction.SOUTH:
                    Dir = 4; break;
                case Direction.WEST:
                    Dir = 6; break;
            }

            Matrix rotMat = Matrix.CreateRotationZ((float)(Dir * Math.PI / 4.0));
            VMPlacementResult[] places = new VMPlacementResult[Objects.Count()];

            var bObj = BaseObject;
            var leadOff = new Vector3(((sbyte)(((ushort)bObj.Object.OBJ.SubIndex) >> 8) * 16), ((sbyte)(((ushort)bObj.Object.OBJ.SubIndex) & 0xFF) * 16), 0);

            //TODO: optimize so we don't have to recalculate all of this
            if (pos != LotTilePos.OUT_OF_WORLD)
            {
                for (int i = 0; i < Objects.Count(); i++)
                {
                    var sub = Objects[i];
                    var off = new Vector3((sbyte)(((ushort)sub.Object.OBJ.SubIndex) >> 8) * 16, (sbyte)(((ushort)sub.Object.OBJ.SubIndex) & 0xFF) * 16, 0);
                    off = Vector3.Transform(off-leadOff, rotMat);

                    var offPos = new LotTilePos((short)Math.Round(pos.x + off.X), (short)Math.Round(pos.y + off.Y), (sbyte)(pos.Level + sub.Object.OBJ.LevelOffset));
                    places[i] = sub.PositionValid(offPos, direction, context);
                    if (places[i].Status != VMPlacementError.Success)
                    {
                        //go back to where we started: we're no longer out of world.
                        for (int j = 0; j < Objects.Count(); j++) Objects[j].PositionChange(context);
                        return places[i].Status;
                    }
                }
            }
            
            //verification success

            for (int i = 0; i < Objects.Count(); i++)
            {
                var sub = Objects[i];
                var off = new Vector3((sbyte)(((ushort)sub.Object.OBJ.SubIndex) >> 8) * 16, (sbyte)(((ushort)sub.Object.OBJ.SubIndex) & 0xFF)*16, 0);
                off = Vector3.Transform(off-leadOff, rotMat);

                var offPos = (pos==LotTilePos.OUT_OF_WORLD)?
                    LotTilePos.OUT_OF_WORLD :
                    new LotTilePos((short)Math.Round(pos.x + off.X), (short)Math.Round(pos.y + off.Y), (sbyte)(pos.Level+sub.Object.OBJ.LevelOffset));

                sub.SetIndivPosition(offPos, direction, context, places[i]);
            }
            for (int i = 0; i < Objects.Count(); i++) Objects[i].PositionChange(context);
            return VMPlacementError.Success;
        }

        public void SetVisualPosition(Vector3 pos, Direction direction, VMContext context)
        {
            int Dir = 0;
            switch (direction)
            {
                case Direction.NORTH:
                    Dir = 0; break;
                case Direction.EAST:
                    Dir = 2; break;
                case Direction.SOUTH:
                    Dir = 4; break;
                case Direction.WEST:
                    Dir = 6; break;
            }

            Matrix rotMat = Matrix.CreateRotationZ((float)(Dir * Math.PI / 4.0));
            var bObj = BaseObject;
            var leadOff = new Vector3((sbyte)(((ushort)bObj.Object.OBJ.SubIndex) >> 8), (sbyte)(((ushort)bObj.Object.OBJ.SubIndex) & 0xFF), 0);

            for (int i = 0; i < Objects.Count(); i++)
            {
                var sub = Objects[i];
                var off = new Vector3((sbyte)(((ushort)sub.Object.OBJ.SubIndex) >> 8), (sbyte)(((ushort)sub.Object.OBJ.SubIndex) & 0xFF), sub.Object.OBJ.LevelOffset*2.95f);
                off = Vector3.Transform(off-leadOff, rotMat);

                sub.Direction = direction;
                sub.VisualPosition = pos + off;
            }
            //for (int i = 0; i < Objects.Count(); i++) Objects[i].PositionChange(context);
        }

        public void ExecuteEntryPoint(int num, VMContext context)
        {
            for (int i = 0; i < Objects.Count; i++) Objects[i].ExecuteEntryPoint(num, context, true);
        }

        public void Delete(VMContext context)
        {
            for (int i = 0; i < Objects.Count(); i++)
            {
                var obj = Objects[i];
                obj.PrePositionChange(context);
                context.RemoveObjectInstance(obj);
            }
        }

        public void Init(VMContext context)
        {
            for (int i = 0; i < Objects.Count(); i++)
            {
                Objects[i].Init(context);
            }
        }
    }
}
