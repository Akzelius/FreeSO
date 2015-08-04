﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSO.Simantics.net.model;
using System.IO;
using GonzoNet;
using GonzoNet.Encryption;
using System.Net;
using ProtocolAbstractionLibraryD;
using TSO.Simantics.net.model.commands;

namespace TSO.Simantics.net.drivers
{
    public class VMServerDriver : VMNetDriver
    {
        private List<VMNetTick> History; //keep all of this until we get restore points
        private List<VMNetCommand> QueuedCmds;

        private const int TICKS_PER_PACKET = 2;
        private List<VMNetTick> TickBuffer;

        private Dictionary<NetworkClient, uint> UIDs;

        private Listener listener;

        private uint TickID = 0;

        public VMServerDriver(int port)
        {
            listener = new Listener(EncryptionMode.NoEncryption);
            listener.Initialize(new IPEndPoint(IPAddress.Any, port));
            listener.OnConnected += SendLotState;
            listener.OnDisconnected += LotDC;

            History = new List<VMNetTick>();
            QueuedCmds = new List<VMNetCommand>();
            TickBuffer = new List<VMNetTick>();
            UIDs = new Dictionary<NetworkClient, uint>();
        }

        private void LotDC(NetworkClient Client)
        {
            lock (UIDs) {
                if (UIDs.ContainsKey(Client)) {
                    uint UID = UIDs[Client];
                    UIDs.Remove(Client);

                    SendCommand(new VMNetSimLeaveCmd
                    {
                        SimID = UID
                    });
                }
            }
        }

        private void SendLotState(NetworkClient client)
        {
            lock (History)
            {
                var ticks = new VMNetTickList { Ticks = History };
                byte[] data;
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        ticks.SerializeInto(writer);
                    }
                    data = stream.ToArray();
                }

                using (var stream = new PacketStream((byte)PacketType.VM_PACKET, 0))
                {
                    stream.WriteHeader();
                    stream.WriteInt32(data.Length + (int)PacketHeaders.UNENCRYPTED);
                    stream.WriteBytes(data);
                    client.Send(stream.ToArray());
                }
            }
        }

        public override void SendCommand(VMNetCommandBodyAbstract cmd)
        {
            lock (QueuedCmds)
            {
                QueuedCmds.Add(new VMNetCommand(cmd));
            }
        }

        public override bool Tick(VM vm)
        {
            HandleClients();

            lock (QueuedCmds) { 
                var tick = new VMNetTick();
                tick.Commands = new List<VMNetCommand>(QueuedCmds);
                tick.TickID = TickID++;
                tick.RandomSeed = vm.Context.RandomSeed;

                InternalTick(vm, tick);
                QueuedCmds.Clear();

                TickBuffer.Add(tick);
                if (TickBuffer.Count >= TICKS_PER_PACKET) SendTickBuffer();
            }

            return true;
        }

        private void SendTickBuffer()
        {
            lock (History)
            {
                History.AddRange(TickBuffer);
            }

            var ticks = new VMNetTickList { Ticks = TickBuffer };
            byte[] data;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    ticks.SerializeInto(writer);
                }

                data = stream.ToArray();
            }

            using (var stream = new PacketStream((byte)PacketType.VM_PACKET, 0))
            {
                stream.WriteHeader();
                stream.WriteInt32(data.Length + (int)PacketHeaders.UNENCRYPTED);
                stream.WriteBytes(data);
                Broadcast(stream.ToArray());
            }

            TickBuffer.Clear();
        }

        private void Broadcast(byte[] packet)
        {
            lock (listener.Clients)
            {
                var clients = new List<NetworkClient>(listener.Clients);
                foreach (var client in clients)
                {
                    client.Send(packet);
                }
            }
        }

        private void HandleClients()
        {
            lock (listener.Clients)
            {
                foreach (var client in listener.Clients)
                {
                    var packets = client.GetPackets();
                    while (packets.Count > 0)
                    {
                        OnPacket(client, packets.Dequeue());
                    }
                }
            }
        }

        public override void OnPacket(NetworkClient client, ProcessedPacket packet)
        {
            var cmd = new VMNetCommand();
            using (var reader = new BinaryReader(packet)) {
                cmd.Deserialize(reader);
            }
            if (cmd.Type == VMCommandType.SimJoin)
            {
                lock (UIDs)
                {
                    UIDs.Add(client, ((VMNetSimJoinCmd)cmd.Command).SimID);
                }
            }
            SendCommand(cmd.Command);
        }

        public override void CloseNet()
        {
            listener.Close();
        }
    }
}
