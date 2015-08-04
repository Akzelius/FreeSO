﻿/*This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.

The Original Code is the TSOClient.

The Initial Developer of the Original Code is
RHY3756547. All Rights Reserved.

Contributor(s): ______________________________________.
*/


using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TSOClient.Code.UI.Framework;
using TSOClient.Code.UI.Framework.Parser;
using TSOClient.Code.UI.Model;
using TSO.Common.rendering.framework.model;
using TSO.Common.rendering.framework.io;
using TSOClient.Code.Utils;
using TSO.Simantics.engine;
using TSO.Simantics;
using TSOClient.LUI;
using TSO.HIT;
using TSO.Simantics.net.model.commands;

namespace TSOClient.Code.UI.Controls
{
    /// <summary>
    /// The queue display for ingame. Includes queue animations and control.
    /// </summary>
    public class UIInteractionQueue : UIContainer
    {

        private List<UIIQTrackEntry> QueueItems;
        public VMEntity QueueOwner;
        public VM vm;
        public Vector2 PieMenuClickPos = new Vector2(-1, -1);

        public UIInteractionQueue(VMEntity QueueOwner, VM vm)
        {
            this.vm = vm;
            this.QueueOwner = QueueOwner;
            QueueItems = new List<UIIQTrackEntry>();
        }

        public override void Update(UpdateState state)
        {
 	        base.Update(state);
            if (QueueOwner == null) return;
            //detect any changes in the interaction queue.

            var queue = QueueOwner.Thread.Queue;
            for (int i=0; i<QueueItems.Count; i++) {
                int position = 0;
                var itemui = QueueItems[i];
                bool found = false; //is this interaction still in the queue? if not then ditch it.
                for (int j = 0; j < queue.Count; j++)
                {
                    var elem = queue[j];
                    if (elem == itemui.Interaction)
                    {
                        found = true;
                        if (position != itemui.QueuePosition) itemui.TweenToPosition(position);
                        if (elem.Cancelled && !itemui.Cancelled)
                        {
                            itemui.Cancelled = true;
                            itemui.UI.SetCancelled();
                        }

                        if (elem.Name != itemui.Name)
                        {
                            itemui.Name = elem.Name;
                            itemui.UI.Tooltip = itemui.Name;
                        }
                        if (j == 0 && !itemui.Active)
                        {
                            itemui.Active = true;
                            itemui.UI.SetActive(true);
                        }

                        if (itemui.IconOwner != elem.IconOwner)
                        {
                            itemui.IconOwner = elem.IconOwner;
                            itemui.UpdateInteractionIcon();
                        }
                        break;
                    }
                    if (elem.Priority != VMQueuePriority.Idle) position++;
                }
                if (!found)
                {
                    this.Remove(itemui.UI);
                    QueueItems.RemoveAt(i--); //not here anymore
                }
                else itemui.Update();
            }

            //now detect if there are any interactions we're not displaying and add them.

            for (int i = 0; i < queue.Count; i++)
            {
                int position = 0;
                var elem = queue[i];

                if (elem.Priority != VMQueuePriority.Idle)
                {
                    bool found = false; //is this interaction in the queue? if not, add it
                    for (int j = 0; j < QueueItems.Count; j++)
                    {
                        var itemui = QueueItems[j];
                        if (elem == itemui.Interaction)
                        {
                            found = true;
                            break;
                        }

                    }
                    if (!found) //new interaction!!!
                    {
                        var itemui = new UIIQTrackEntry() {
                            Interaction = elem,
                            IconOwner = elem.IconOwner,
                            SourcePos = (PieMenuClickPos.X < 0)?(new Vector2(30 + position * 50, 30)):PieMenuClickPos,
                            TweenProgress = 0,
                            UI = new UIInteraction(i==0),
                            Active = (i == 0)
                        };
                        itemui.UI.OnMouseEvent += new ButtonClickDelegate(InteractionClicked);
                        itemui.UI.ParentEntry = itemui;
                        itemui.Name = elem.Name;
                        itemui.UI.Tooltip = itemui.Name;
                        itemui.TweenToPosition(position);
                        itemui.UpdateInteractionIcon();
                        itemui.Update();
                        this.Add(itemui.UI);
                        QueueItems.Add(itemui);

                        PieMenuClickPos = new Vector2(-1, -1);
                    }
                    position++;
                }
            }

        }

        private void InteractionClicked(UIElement ui)
        {
            UIInteraction item = (UIInteraction)ui;
            var itemui = item.ParentEntry;
            var queue = QueueOwner.Thread.Queue;
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i] == itemui.Interaction)
                {
                    HITVM.Get().PlaySoundEvent(UISounds.QueueDelete);
                    if (!itemui.Interaction.Cancelled)
                    {
                        vm.SendCommand(new VMNetInteractionCancelCmd
                        {
                            ActionUID = itemui.Interaction.UID,
                            CallerID = QueueOwner.ObjectID
                        });
                    }
                    break;
                }
            }
        }
    }

    public class UIIQTrackEntry //this class basically keeps track of states to determine if things have changed.
    {
        public VMQueuedAction Interaction;
        public UIInteraction UI;
        public VMEntity IconOwner;
        public int QueuePosition;
        public bool Active;
        public bool Cancelled;
        public string Name;

        public double TweenProgress;
        public Vector2 TargetPos;
        public Vector2 SourcePos;
        public double MotionPerFrame = 1.0 / 25.0; //default to finishing in 25 frames

        public void TweenToPosition(int pos) {
            QueuePosition = pos;
            SourcePos = GetTweenPosition();
            TargetPos = new Vector2(30 + pos * 50, 30);
            TweenProgress = 0;
        }

        private Vector2 GetTweenPosition()
        {
            return TargetPos * (float)TweenProgress + SourcePos * (1 - (float)TweenProgress);
        }

        public void Update()
        {
            if (TweenProgress < 1) {
                TweenProgress = Math.Min(TweenProgress + MotionPerFrame, 1);
                UI.Position = GetTweenPosition();
            }
        }

        public void UpdateInteractionIcon()
        {
            UI.Icon = IconOwner.GetIcon(GameFacade.GraphicsDevice);
        }
    }
}
