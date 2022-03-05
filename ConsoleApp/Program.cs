using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleApp
{
    class TrafficData
    {

    }

    public class Program
    {
        private static string _csvFileName;

        private static CaptureFileWriterDevice captureFileWriter;
        private static int packetIndex_option4 = 0;

        public static void Main()
        {
            bool isCorrectOption = false;

            while (!isCorrectOption)
            {
                Console.WriteLine("Select option:");
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine("1. Print traffic features from interfaces");
                Console.WriteLine("2. Print traffic features from capture files");
                Console.WriteLine("3. Print traffic features from Csv Database");
                Console.WriteLine("4. Create capture file from real-time traffic");

                int option = Convert.ToInt32(Console.ReadLine());

                switch (option)
                {
                    case 1:
                        PrintTrafficFaturesFromInterface();
                        isCorrectOption = true;
                        break;
                    case 2:
                        PrintTrafficFaturesFromCaptureFile();
                        isCorrectOption = true;
                        break;
                    case 3:
                        PrintTrafficFaturesFromCsvDatabase();
                        isCorrectOption = true;
                        break;
                    case 4:
                        CreateCaptureFileFromRealTimeTraffic();
                        isCorrectOption = true;
                        break;
                    default:
                        break;
                }
            }

        }

        private static void PrintTrafficFaturesFromInterface()
        {
            /* Retrieve the device list */
            var devices = CaptureDeviceList.Instance;

            /*If no device exists, print error */
            if (devices.Count < 1)
            {
                Console.WriteLine("No device found on this machine");
                return;
            }

            Console.WriteLine("The following devices are available on this machine:");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine();

            int i = 0;

            /* Scan the list printing every entry */
            foreach (var dev in devices)
            {
                /* Description */
                Console.WriteLine("{0}) {1} {2}", i, dev.Name, dev.Description);
                i++;
            }

            Console.WriteLine();
            Console.Write("-- Please choose a device to capture: ");
            i = int.Parse(Console.ReadLine());

            using var device = devices[i];

            //Register our handler function to the 'packet arrival' event
            device.OnPacketArrival +=
                new PacketArrivalEventHandler(device_OnPacketArrival_option1);

            // Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(DeviceModes.Promiscuous, readTimeoutMilliseconds);

            Console.WriteLine("-- Listening on {0}, hit 'Ctrl-C' to exit...",
                              device.Description);

            // Start capture 'INFINTE' number of packets
            device.Capture();
        }

        /// <summary>
        /// Prints the time, length, src ip, src port, dst ip and dst port
        /// for each TCP/IP packet received on the network
        /// </summary>
        private static void device_OnPacketArrival_option1(object sender, PacketCapture e)
        {
            var time = e.Header.Timeval.Date;
            var len = e.Data.Length;
            var rawPacket = e.GetPacket();

            var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            var ipPacket = packet.Extract<PacketDotNet.IPPacket>();

            string protocol = "";
            if (ipPacket != null)
                protocol = ipPacket.Protocol.ToString();

            switch (protocol.ToUpper())
            {
                case "TCP":
                    var tcpPacket = packet.Extract<PacketDotNet.TcpPacket>();
                    if (tcpPacket != null)
                    {
                        System.Net.IPAddress srcIp = ipPacket.SourceAddress;
                        System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
                        int srcPort = tcpPacket.SourcePort;
                        int dstPort = tcpPacket.DestinationPort;

                        Console.WriteLine("Type: TCP" +
                                          "\r\nSource address: " + srcIp +
                                          "\r\nSource port: " + srcPort +
                                          "\r\nDestination address: " + dstIp +
                                          "\r\nDestination port: " + dstPort +
                                          "\r\nTCP header size: " + tcpPacket.DataOffset +
                                          "\r\nWindow size: " + tcpPacket.WindowSize +
                                          "\r\nChecksum: " + tcpPacket.Checksum.ToString() + (tcpPacket.ValidChecksum ? ",valid" : ",invalid") +
                                          "\r\nTCP checksum: " + (tcpPacket.ValidTcpChecksum ? "valid" : "invalid") +
                                          "\r\nSequence number: " + tcpPacket.SequenceNumber.ToString() +
                                          "\r\nAcknowledgment number: " + tcpPacket.AcknowledgmentNumber + (tcpPacket.Acknowledgment ? ",valid" : ",invalid") +
                                          // flags
                                          "\r\nUrgent pointer: " + (tcpPacket.Urgent ? "valid" : "invalid") +
                                          "\r\nACK flag: " + (tcpPacket.Acknowledgment ? "1" : "0") +
                                          "\r\nPSH flag: " + (tcpPacket.Push ? "1" : "0") +
                                          "\r\nRST flag: " + (tcpPacket.Reset ? "1" : "0") +
                                          "\r\nSYN flag: " + (tcpPacket.Synchronize ? "1" : "0") +
                                          "\r\nFIN flag: " + (tcpPacket.Finished ? "1" : "0") +
                                          "\r\nECN flag: " + (tcpPacket.ExplicitCongestionNotificationEcho ? "1" : "0") +
                                          "\r\nCWR flag: " + (tcpPacket.CongestionWindowReduced ? "1" : "0") +
                                          "\r\nNS flag: " + (tcpPacket.NonceSum ? "1" : "0") +
                                          "\r\n");
                    }
                    break;
                case "UDP":
                    var udpPacket = packet.Extract<PacketDotNet.UdpPacket>();
                    if (udpPacket != null)
                    {
                        System.Net.IPAddress srcIp = ipPacket.SourceAddress;
                        System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
                        int srcPort = udpPacket.SourcePort;
                        int dstPort = udpPacket.DestinationPort;

                        Console.WriteLine("Type: UDP" +
                                          "\r\nSource address: " + srcIp +
                                          "\r\nSource port: " + srcPort +
                                          "\r\nDestination address: " + dstIp +
                                          "\r\nDestination port: " + dstPort +
                                          "\r\nChecksum: " + udpPacket.Checksum.ToString() + " valid: " + udpPacket.ValidChecksum +
                                          "\r\nValid UDP checksum: " + udpPacket.ValidUdpChecksum +
                                          "\r\n");
                    }
                    break;
                case "ARP":
                    var arpPacket = packet.Extract<PacketDotNet.ArpPacket>();
                    if (arpPacket != null)
                    {
                        System.Net.IPAddress senderAddress = arpPacket.SenderProtocolAddress;
                        System.Net.IPAddress targetAddress = arpPacket.TargetProtocolAddress;
                        System.Net.NetworkInformation.PhysicalAddress senderHardwareAddress = arpPacket.SenderHardwareAddress;
                        System.Net.NetworkInformation.PhysicalAddress targetHardwareAddress = arpPacket.TargetHardwareAddress;

                        Console.WriteLine("Type: ARP" +
                                          "\r\nSender protocol address: " + senderAddress +
                                          "\r\nTarget protocol address: " + targetAddress +
                                          "\r\nSender hardware address: " + senderHardwareAddress +
                                          "\r\nTarget hardware address: " + targetHardwareAddress +
                                          "\r\nHardware address length: " + arpPacket.HardwareAddressLength +
                                          "\r\nProtocol address lenth: " + arpPacket.ProtocolAddressLength +
                                          "\r\nOperation: " + arpPacket.Operation.ToString() +
                                          "\r\n");
                    }
                    break;
                case "ICMP":
                    var icmpPacket = packet.Extract<PacketDotNet.IcmpV4Packet>();
                    if (icmpPacket != null)
                    {
                        Console.WriteLine("Type: ICMP v4" +
                                          "\r\nType Code: 0x" + icmpPacket.TypeCode.ToString("x") +
                                          "\r\nChecksum: " + icmpPacket.Checksum.ToString("x") +
                                          "\r\nID: 0x" + icmpPacket.Id.ToString("x") +
                                          "\r\nSequence number: " + icmpPacket.Sequence.ToString() +
                                          "\r\n");
                    }
                    break;
                case "IGMP":
                    var igmpPacket = packet.Extract<PacketDotNet.IgmpV2Packet>();
                    if (igmpPacket != null)
                    {
                        Console.WriteLine("Type: IGMP v2" +
                                          "\r\nType: " + igmpPacket.Type +
                                          "\r\nGroup address: " + igmpPacket.GroupAddress +
                                          "\r\nMax response time: " + igmpPacket.MaxResponseTime +
                                          "\r\n");
                    }
                    break;
                default:
                    break;
            }
        }

        private static void PrintTrafficFaturesFromCaptureFile()
        {
            // read the file from stdin or from the command line arguments
            string capFile;
            Console.Write("-- Please enter an input capture file name: ");
            capFile = Console.ReadLine();

            Console.WriteLine("opening '{0}'", capFile);

            ICaptureDevice device;

            try
            {
                // Get an offline device
                device = new CaptureFileReaderDevice(capFile);

                // Open the device
                device.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("Caught exception when opening file" + e.ToString());
                return;
            }

            // Register our handler function to the 'packet arrival' event
            device.OnPacketArrival +=
                new PacketArrivalEventHandler(device_OnPacketArrival_option1);

            Console.WriteLine();
            Console.WriteLine
                ("-- Capturing from '{0}', hit 'Ctrl-C' to exit...",
                capFile);

            // Start capture 'INFINTE' number of packets
            // This method will return when EOF reached.
            device.Capture();

            // Close the pcap device
            device.Close();

            Console.Write("Hit 'Enter' to exit...");
            Console.ReadLine();
        }

        private static void PrintTrafficFaturesFromCsvDatabase()
        {
            string csvFileName;
            Console.Write("-- Please enter an input csv file name: ");
            csvFileName = Console.ReadLine();

            List<List<object>> featuresList = new List<List<object>>();

            using (var reader = new StreamReader(csvFileName))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');

                    featuresList.Add(new List<object>(values));
                }
            }

            // Print features
            foreach (List<object> subList in featuresList)
            {
                foreach (object feature in subList)
                {
                    Console.WriteLine(feature);
                }
                Console.WriteLine("-----------------------------");
            }
        }

        private static void CreateCaptureFileFromRealTimeTraffic()
        {
            // Retrieve the device list
            var devices = LibPcapLiveDeviceList.Instance;

            // If no devices were found print an error
            if (devices.Count < 1)
            {
                Console.WriteLine("No devices were found on this machine");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("The following devices are available on this machine:");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine();

            int i = 0;

            // Print out the devices
            foreach (var dev in devices)
            {
                /* Description */
                Console.WriteLine("{0}) {1} {2}", i, dev.Name, dev.Description);
                i++;
            }

            Console.WriteLine();
            Console.Write("-- Please choose a device to capture on: ");
            i = int.Parse(Console.ReadLine());
            Console.Write("-- Please enter the output file name: ");
            string capFile = Console.ReadLine();

            using var device = devices[i];

            // Register our handler function to the 'packet arrival' event
            device.OnPacketArrival +=
                new PacketArrivalEventHandler(device_OnPacketArrival_option4);

            // Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(mode: DeviceModes.Promiscuous | DeviceModes.DataTransferUdp | DeviceModes.NoCaptureLocal, read_timeout: readTimeoutMilliseconds);

            Console.WriteLine();
            Console.WriteLine("-- Listening on {0} {1}, writing to {2}, hit 'Enter' to stop...",
                              device.Name, device.Description,
                              capFile);

            // open the output file
            captureFileWriter = new CaptureFileWriterDevice(capFile);
            captureFileWriter.Open(device);

            // Start the capturing process
            device.StartCapture();

            // Wait for 'Enter' from the user.
            Console.ReadLine();

            // Stop the capturing process
            device.StopCapture();

            Console.WriteLine("-- Capture stopped.");

            // Print out the device statistics
            Console.WriteLine(device.Statistics.ToString());
        }

        /// <summary>
        /// // Write the packet to the file
        /// </summary>
        private static void device_OnPacketArrival_option4(object sender, PacketCapture e)
        {
            //var device = (ICaptureDevice)sender;

            // Write the packet to the file
            var rawPacket = e.GetPacket();
            captureFileWriter.Write(rawPacket);
            Console.WriteLine("Packet dumped to file.");

            if (rawPacket.LinkLayerType == PacketDotNet.LinkLayers.Ethernet)
            {
                var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                var ethernetPacket = (EthernetPacket)packet;

                Console.WriteLine("{0} At: {1}:{2}: MAC:{3} -> MAC:{4}",
                                  packetIndex_option4,
                                  rawPacket.Timeval.Date.ToString(),
                                  rawPacket.Timeval.Date.Millisecond,
                                  ethernetPacket.SourceHardwareAddress,
                                  ethernetPacket.DestinationHardwareAddress);
                packetIndex_option4++;
            }
        }
    }
}
