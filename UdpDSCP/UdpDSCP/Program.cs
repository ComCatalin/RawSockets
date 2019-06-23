using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

/*

UDP Field:
 0      7 8     15 16    23 24    31 
+--------+--------+--------+--------+
|      Source     |    Destination  |
|       Port      |       Port      |
+--------+--------+--------+--------+
|      Length     |     Checksum    |
+--------+--------+--------+--------+
|
|        data octets ...
+--------------- ...

UDP Pseudo Header
 0      7 8     15 16    23 24    31 
+--------+--------+--------+--------+
|           source address          |
+--------+--------+--------+--------+
|        destination address        |
+--------+--------+--------+--------+
|  zero  |protocol|   UDP length    |
+--------+--------+--------+--------+

IP Header
 0      7 8     15 16    23 24    31
+--------+--------+--------+--------+
|Ver.|IHL|DSCP|ECN|   Total length  |
+--------+--------+--------+--------+
|  Identification |Flags|   Offset  |
+--------+--------+--------+--------+
|   TTL  |Protocol| Header Checksum |
+--------+--------+--------+--------+
|         Source IP address         |
+--------+--------+--------+--------+
|       Destination IP address      |
+--------+--------+--------+--------+

    The IPv4 header checksum is a simple checksum used in version 4 of the Internet Protocol (IPv4) to protect the header of IPv4 data packets
against data corruption. This checksum is calculated only for the header bytes (with the checksum bytes set to 0), is 16 bits long

    Since the header length is not constant, a field in the header, IHL, is provided
to tell how long the header is, in 32-bit words. The minimum value is 5, which
applies when no options are present. The maximum value of this 4-bit field is 15,
which limits the header to 60 bytes, and thus the Options field to 40 bytes. For
some options, such as one that records the route a packet has taken, 40 bytes is far
too small, making those options useless.

    No IP options. The total length will be 5.
Today, IP options have fallen out of favor. Many routers ignore them or do
not process them efficiently, shunting them to the side as an uncommon case. That
is, they are only partly supported and they are rarely used.

    The Differentiated services field is one of the few fields that has changed its
meaning (slightly) over the years. Originally, it was called the Type of service
field. It was and still is intended to distinguish between different classes of service.
Various combinations of reliability and speed are possible. For digitized
voice, fast delivery beats accurate delivery. For file transfer, error-free transmission
is more important than fast transmission. The Type of service field provided
3 bits to signal priority and 3 bits to signal whether a host cared more about delay,
throughput, or reliability. However, no one really knew what to do with these bits
at routers, so they were left unused for many years. When differentiated services
were designed, IETF threw in the towel and reused this field. Now, the top 6 bits
are used to mark the packet with its service class; we described the expedited and
assured services earlier in this chapter. The bottom 2 bits are used to carry explicit
congestion notification information, such as whether the packet has experienced
congestion.

*/


namespace UdpDSCP
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourceEndpoint = new IPEndPoint(IPAddress.Loopback, 35869);
            var destinationEndpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var data = Encoding.ASCII.GetBytes("Hello!");
            SendRaw(data, sourceEndpoint, destinationEndpoint);
            Send(data, sourceEndpoint, destinationEndpoint);
        }

        private static byte[] Encapsulate(byte[] data, IPEndPoint source, IPEndPoint destination)
        {
            if (source.Address.AddressFamily != AddressFamily.InterNetwork || destination.Address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException();
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                var sourceAddrBytes = source.Address.GetAddressBytes();
                var destinationAddrBytes = destination.Address.GetAddressBytes();
                var sourceAddrChecksum = (ushort)((ushort)(sourceAddrBytes[0] << 8) + sourceAddrBytes[1] + (ushort)(sourceAddrBytes[2] << 8) + sourceAddrBytes[3]);
                var destinationAddrChecksum = (ushort)((ushort)(destinationAddrBytes[0] << 8) + destinationAddrBytes[1] + (ushort)(destinationAddrBytes[2] << 8) + destinationAddrBytes[3]);

                const ushort udpHeaderLength = 8;
                byte ihl = 5; // header length
                byte version = 4;
                byte versionByte = (byte)((version << 4) | (ihl & 15));

                byte dscp = 8;
                byte ecn = 0;
                byte dscpByte = (byte)((dscp << 2) | (ecn & 3));

                ushort totalLength = Be((ushort)(ihl * 4 + udpHeaderLength + data.Length)); // ip header + udp header + data

                //Identification (fragments that belong togheter)
                ushort identification = Be(1);

                // we will send only a small datagram. Not fragmented
                ushort flags = Be(0);

                byte ttl = 128;
                byte protocol = (byte)ProtocolType.Udp;

                ushort headerChecksum = (ushort)~((versionByte << 8) + dscpByte + totalLength + identification + (ttl << 8) + protocol + sourceAddrChecksum + destinationAddrChecksum);
                headerChecksum = Be(headerChecksum);

                bw.Write(versionByte); bw.Write(dscpByte);
                bw.Write(totalLength);
                bw.Write(identification);
                bw.Write(flags);
                bw.Write(ttl);
                bw.Write(protocol);
                bw.Write(headerChecksum);

                //source ip address
                bw.Write(source.Address.GetAddressBytes());
                //destination ip address
                bw.Write(destination.Address.GetAddressBytes());


                /////////////////////
                /// UDP part
                /////////////////////

                bw.Write(Be((ushort)source.Port));
                bw.Write(Be((ushort)destination.Port));

                // UDP packet length
                bw.Write(Be((ushort)(udpHeaderLength + data.Length)));

                ushort checksum = 0;
                bw.Write(checksum);

                bw.Write(data);

                bw.Flush();
                return ms.ToArray();
            }
        }

        private static void SendRaw(byte[] data, IPEndPoint source, IPEndPoint destination)
        {
            var packet = Encapsulate(data, source, destination);

            try
            {
                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Udp);

                // We will also provide the header
                s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                s.SendTo(packet, new IPEndPoint(IPAddress.Loopback, 12345));
            }
            catch (Exception se)
            {
                Console.WriteLine(se.Message);
            }
        }

        private static void Send(byte[] data, IPEndPoint source, IPEndPoint destination)
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.ExclusiveAddressUse = false;
                    s.DontFragment = true;
                    s.Bind(source);
                    s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, new byte[] { 8 });
                    s.SendTo(data, destination);
                }
            }
            catch(Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
        }

        private static ushort Be(ushort v)
        {
            var l = (ushort)(v << 8);
            var r = (ushort)(v >> 8);
            return (ushort)(r + l);
        }
    }
}
