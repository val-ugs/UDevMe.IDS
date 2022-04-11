using IDS.Domain.Abstractions;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.Globalization;
using System.IO;

namespace IDS.DataAccess.PCAP
{
    public class DataRepository : IDataRepository
    {
        private const int NumberOfBitsInBytes = 8;

        private static DateTime _lastPacketStartTime = DateTime.Now;
        private readonly string _path;
        Dictionary<(string, int), string> _services;

        public DataRepository(string path)
        {
            _path = path;
            _services = FillServices();
        }

        public string[] GetFilenameList()
        {
            return Directory.GetFiles(_path).Select(file => Path.GetFileName(file)).ToArray();
        }

        private Dictionary<(string, int), string> FillServices()
        {
            Dictionary<(string, int), string> services = new Dictionary<(string, int), string>();
            string path = "..\\..\\..\\..\\IDS.DataAccess.PCAP\\Configuration\\services.csv";

            using (var reader = new StreamReader(path))
            {
                string headerRow = reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    string dataRow = reader.ReadLine();
                    string[] dataMembers = dataRow.Split(",");

                    if (int.TryParse(dataMembers[1], out int value))
                    {
                        if (services.ContainsKey((dataMembers[0], value)))
                            services[(dataMembers[0], value)] += ", " + dataMembers[2];
                        else
                            services.Add((dataMembers[0], value), dataMembers[2]);
                    }  
                }
            }

            return services;
        }

        public List<string[]> GetData(string fileName, bool hasHeaderRow)
        {
            string fullpath = _path + "\\" + fileName;
            ICaptureDevice device;

            try
            {
                // Get an offline device
                device = new CaptureFileReaderDevice(fullpath);

                // Open the device
                device.Open();
            }
            catch (Exception e)
            {
                throw new Exception("Caught exception when opening file" + e.ToString());
            }

            List<(string, Packet)> packetsWithTimestamp = new List<(string, Packet)>();

            // Register our handler function to the 'packet arrival' event
            device.OnPacketArrival +=
                new PacketArrivalEventHandler((sender, e) => device_OnPacketArrival_option(sender, e,
                                                                                           ref packetsWithTimestamp));

            // Start capture 'INFINITE' number of packets
            // This method will return when EOF reached.
            device.Capture();

            // Close the pcap device
            device.Close();

            List<object[]> connections = InitializeConnections(packetsWithTimestamp);

            List<Packet> packets = packetsWithTimestamp.Select(p => p.Item2).ToList();

            List<string[]> data = new List<string[]>(connections.Count);

            for (int i = 0; i < connections.Count; i++)
            {
                object[] contentData = GetContentData(packets);
                object[] hostTraffic = DeriveHostFeatures(connections[i], i, connections, connections.Count);

                data.Add(new string[]
                {
                    connections[i][8].ToString(), // protocol
                    connections[i][9].ToString(), // service
                    connections[i][11].ToString(), // srcBytes
                    connections[i][12].ToString(), // dstBytes
                    connections[i][13].ToString(), // land
                    connections[i][14].ToString(), // wrongFrag
                    connections[i][16].ToString(), // srcPackets
                    connections[i][17].ToString(), // dstPackets
                    connections[i][18].ToString(), // srcTtl
                    connections[i][19].ToString(), // dstTtl
                    connections[i][20].ToString(), // srcLoad
                    connections[i][21].ToString(), // dstLoad
                    contentData[0].ToString(), // hot
                    contentData[2].ToString(), // loggedIn
                    contentData[5].ToString(), // suAttempted
                    contentData[6].ToString(), // numRoot
                    hostTraffic[7].ToString(), // srvRerrorRate
                    hostTraffic[8].ToString(), // srvDiffHostRate
                    hostTraffic[13].ToString(), // dstHostSameSrcPortRate
                    hostTraffic[14].ToString(), // dstHostSrvDiffHostRate
                    hostTraffic[15].ToString(), // dstHostSerrorRate
                    hostTraffic[16].ToString(), // dstHostSrvSerrorRate

                    /*
                    connections[i][7].ToString(), // duration
                    connections[i][8].ToString(), // protocol
                    connections[i][9].ToString(), // service
                    connections[i][10].ToString(), // flag
                    connections[i][11].ToString(), // srcBytes
                    connections[i][12].ToString(), // dstBytes
                    connections[i][13].ToString(), // land
                    connections[i][14].ToString(), // wrongFrag
                    connections[i][15].ToString(), // urgent
                    contentData[0].ToString(), // hot
                    contentData[1].ToString(), // numFailedLogins
                    contentData[2].ToString(), // loggedIn
                    contentData[3].ToString(), // numCompromised
                    contentData[4].ToString(), // rootShell
                    contentData[5].ToString(), // suAttempted
                    contentData[6].ToString(), // numRoot
                    contentData[7].ToString(), // numFileCreations
                    contentData[8].ToString(), // numAccessFiles
                    contentData[9].ToString(), // numOutboundCmds
                    contentData[10].ToString(), // isHotLogin
                    contentData[11].ToString(), // isGuestLogin
                    hostTraffic[0].ToString(), // count
                    hostTraffic[1].ToString(), // serrorRate
                    hostTraffic[2].ToString(), // rerrorRate
                    hostTraffic[3].ToString(), // sameSrvRate
                    hostTraffic[4].ToString(), // diffSrvRate
                    hostTraffic[5].ToString(), // srvCount
                    hostTraffic[6].ToString(), // srvSerrorRate
                    hostTraffic[7].ToString(), // srvRerrorRate
                    hostTraffic[8].ToString(), // srvDiffHostRate
                    hostTraffic[9].ToString(), // dstHostCount
                    hostTraffic[10].ToString(), // dstHostSrvCount
                    hostTraffic[11].ToString(), // dstHostSameSrvRate
                    hostTraffic[12].ToString(), // dstHostDiffSrvRate
                    hostTraffic[13].ToString(), // dstHostSameSrcPortRate
                    hostTraffic[14].ToString(), // dstHostSrvDiffHostRate
                    hostTraffic[15].ToString(), // dstHostSerrorRate
                    hostTraffic[16].ToString(), // dstHostSrvSerrorRate
                    hostTraffic[17].ToString(), // dstHostRerrorRate
                    hostTraffic[18].ToString() // dstHostSrvRerrorRate
                    */
                });
            }

            return data;
        }

        private static void device_OnPacketArrival_option(object sender, PacketCapture e,
                                                          ref List<(string, Packet)> packetsWithTimestamp)
        {
            var rawPacket = e.GetPacket();
            Packet packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            string timestamp = e.Header.Timeval.Date.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                                              CultureInfo.InvariantCulture);
            packetsWithTimestamp.Add((timestamp, packet));
        }

        private List<object[]> InitializeConnections(List<(string, Packet)> packetsWithTime)
        {
            List<object[]> connections = new List<object[]>();
            List<Packet> packets = packetsWithTime.Select(p => p.Item2).ToList();

            for (int i = 0; i < packetsWithTime.Count; i++)
            {
                var item = packetsWithTime.ElementAt(i);
                string timestamp = item.Item1;
                Packet packet = item.Item2;
                int srcBytes = 0;
                int dstBytes = 0;
                int wrongFrag = 0;
                int urgent = 0;
                int srcPackets = 0;
                int dstPackets = 0;
                double srcTtl = 0;
                double dstTtl = 0;
                double srcTime = 0;
                double dstTime = 0;
                const double constantTime = 0.1;
                double srcLoad, dstLoad;

                DateTime startTime = (i == 0) ? DateTime.Parse(timestamp) 
                                              : DateTime.Parse(packetsWithTime.ElementAt(i - 1).Item1);
                DateTime endTime = DateTime.Parse(timestamp);
                double duration = (endTime - startTime).TotalSeconds;

                int srcPort = 0, dstPort = 0;
                string service = "";

                var ipPacket = packet.Extract<IPPacket>();
                string protocol = "";
                if (ipPacket != null)
                    protocol = ipPacket.Protocol.ToString();

                if (protocol.ToUpper() == "TCP")
                {
                    var tcpPacket = packet.Extract<TcpPacket>();
                    if (tcpPacket != null)
                    {
                        srcPort = tcpPacket.SourcePort;
                        dstPort = tcpPacket.DestinationPort;

                        string ianaService = srcPort <= dstPort ? GetIana((protocol, srcPort)) 
                                                                : GetIana((protocol, dstPort));
                        if (ianaService == null)
                            service = "Unassigned";
                        else
                            service = ianaService;
                    }
                }
                else if (protocol.ToUpper() == "UDP")
                {
                    var udpPacket = packet.Extract<UdpPacket>();
                    if (udpPacket != null)
                    {
                        srcPort = udpPacket.SourcePort;
                        dstPort = udpPacket.DestinationPort;

                        string ianaService = service = srcPort <= dstPort ? GetIana((protocol, srcPort))
                                                                          : GetIana((protocol, dstPort));
                        if (ianaService == null)
                            service = "Unassigned";
                        else
                            service = ianaService;
                    }
                }
                else if (protocol.ToUpper() == "ICMP")
                {
                    var icmpPacket = packet.Extract<IcmpV4Packet>();
                    if (icmpPacket != null)
                    {
                        service = "eco_i";
                    }
                }
                else
                    continue;

                IPAddress srcIp = null, dstIp = null;

                int index = 0;
                string statusFlag = "";

                var ipv4Packet = packet.Extract<IPv4Packet>();
                if (ipv4Packet != null)
                {
                    srcIp = ipv4Packet.SourceAddress;
                    dstIp = ipv4Packet.DestinationAddress;
                    index = GetIpAddressIndex(dstIp);
                    statusFlag = GetConnectionStatus(ipPacket, packets.ToList());
                }
                var ipv6Packet = packet.Extract<IPv6Packet>();
                if (ipv6Packet != null)
                {
                    srcIp = ipv6Packet.SourceAddress;
                    dstIp = ipv6Packet.DestinationAddress;
                    index = GetIpAddressIndex(dstIp, isIpv4: false);
                    statusFlag = GetConnectionStatus(ipPacket, packets.ToList(), isIpv4: false);
                }

                int land;

                if (srcIp == dstIp && srcPort == dstPort)
                    land = 1;
                else
                    land = 0;

                srcTtl = ipPacket.TimeToLive;

                for (int j = 0; j < packets.Count; j++)
                {
                    var otherIpPacket = packets[j].Extract<IPPacket>();
                    string otherIpPacketTimestamp = packetsWithTime[j].Item1;
                    DateTime otherStartTime = (i == 0) ? DateTime.Parse(timestamp)
                                                       : DateTime.Parse(packetsWithTime.ElementAt(i - 1).Item1);
                    DateTime otherEndTime = DateTime.Parse(timestamp);
                    double otherDuration = (endTime - startTime).TotalSeconds;

                    if (otherIpPacket != null)
                    {
                        if (srcIp.ToString() == otherIpPacket.SourceAddress.ToString())
                        {
                            srcBytes += otherIpPacket.Bytes.Length;
                            srcTime += otherDuration;
                        }
                        else
                        {
                            dstBytes += otherIpPacket.Bytes.Length;
                            dstTime += otherDuration;
                        }

                        if (protocol.ToUpper() == "TCP" && otherIpPacket.Protocol.ToString().ToUpper() == "TCP")
                        {
                            var otherTcpPacket = packet.Extract<TcpPacket>();
                            urgent += otherTcpPacket.Urgent ? 1 : 0;
                            wrongFrag += otherTcpPacket.ValidTcpChecksum ? 1 : 0;
                        }
                        else if (protocol.ToUpper() == "UDP" && otherIpPacket.Protocol.ToString().ToUpper() == "UDP")
                        {
                            var otherUdpPacket = packet.Extract<UdpPacket>();
                            wrongFrag += otherUdpPacket.ValidUdpChecksum ? 1 : 0;
                        }

                        if (srcIp.ToString() == otherIpPacket.SourceAddress.ToString()
                            || dstIp.ToString() == otherIpPacket.DestinationAddress.ToString())
                            srcPackets++;

                        if (srcIp.ToString() == otherIpPacket.DestinationAddress.ToString()
                            || dstIp.ToString() == otherIpPacket.SourceAddress.ToString())
                        {
                            dstPackets++;
                            if (dstTtl == 0)
                                dstTtl = otherIpPacket.TimeToLive;
                        }
                    }
                }

                srcLoad = srcBytes * NumberOfBitsInBytes / (srcTime + constantTime);
                dstLoad = dstBytes * NumberOfBitsInBytes / (dstTime + constantTime);

                object[] connectionRow = new object[]
                {
                    timestamp, srcIp, srcPort, dstIp, dstPort, index, i,
                    duration, protocol, service, statusFlag, srcBytes,
                    dstBytes, land, wrongFrag, urgent,
                    srcPackets, dstPackets, srcTtl, dstTtl, srcLoad, dstLoad
                };
                connections.Add(connectionRow);
            }

            return connections;
        }

        private string GetIana((string, int) serviceOption)
        {
            return _services.FirstOrDefault(
                s => (s.Key.Item1.ToUpper() == serviceOption.Item1.ToUpper()
                      && s.Key.Item2 == serviceOption.Item2)
            ).Value;
        }

        private int GetIpAddressIndex(IPAddress ipAddress, bool isIpv4 = true)
        {
            int power = 0;
            int index = 0;
            if (isIpv4)
            {
                string[] ipParts = ipAddress.ToString().Split('.');
                ipParts.Reverse();
                foreach (string ipPart in ipParts)
                {
                    if (int.TryParse(ipPart, out int value))
                    {
                        index += value * (int)Math.Pow(10, power);
                        power += 3;
                    }
                }
            }
            else
            {
                index = 1;
            }

            return index;
        }

        private string GetConnectionStatus(IPPacket ipPacket, List<Packet> packets, bool isIpv4 = true)
        {
            string protocol = ipPacket.Protocol.ToString();
            if (protocol.ToUpper() == "UDP" || protocol.ToUpper() == "ICMP")
                return "SF";

            Dictionary<string, Dictionary<string, string>> connectionFlags = GetConnectionFlags();

            IPAddress srcIp = ipPacket.SourceAddress;
            string connectionStatus = "INIT";

            foreach (Packet packet in packets)
            {
                StringBuilder key = new StringBuilder();
                var otherIpPacket = packet.Extract<IPPacket>();
                if (otherIpPacket != null)
                {
                    TcpPacket otherTcpPacket = otherIpPacket.Extract<TcpPacket>();
                    if (otherTcpPacket != null)
                    {
                        if (srcIp.ToString() == otherIpPacket.SourceAddress.ToString())
                            key.Append("1");
                        else
                            key.Append("0");
                        key.Append(otherTcpPacket.Synchronize ? "1" : "0");
                        key.Append(otherTcpPacket.Acknowledgment ? "1" : "0");
                        key.Append(otherTcpPacket.Reset ? "1" : "0");
                        key.Append(otherTcpPacket.Finished ? "1" : "0");

                        if (connectionFlags[connectionStatus].ContainsKey(key.ToString()) == false)
                        {
                            if (connectionStatus == "INIT")
                                return "OTH";
                            else if (connectionStatus == "SH" || connectionStatus == "SHR")
                                return connectionStatus;
                            else if (connectionStatus == "RSTRH" || connectionStatus == "OTH")
                                return connectionStatus;
                            else if (connectionStatus == "REJ" || connectionStatus == "RST0S0" || connectionStatus == "RST0")
                                return connectionStatus;
                            else if (connectionStatus == "RSTR" || connectionStatus == "SF")
                                return connectionStatus;
                            else
                                continue;
                        }
                        if (connectionFlags[connectionStatus][key.ToString()] != null)
                            connectionStatus = connectionFlags[connectionStatus][key.ToString()];
                    }
                }
            }

            return connectionStatus;
        }

        private Dictionary<string, Dictionary<string, string>> GetConnectionFlags()
        {
            return new Dictionary<string, Dictionary<string, string>>()
            {
                {
                    "INIT", new Dictionary<string, string>()
                    {
                        { "01100", "S4" },
                        { "10001", "SH" },
                        { "11000", "S0" }
                    }
                },
                {
                    "S4", new Dictionary<string, string>()
                    {
                        { "00010", "SHR" },
                        { "00001", "RSTRH" }
                    }
                },
                {
                    "SH", new Dictionary<string, string>() { }
                },
                {
                    "SHR", new Dictionary<string, string>() { }
                },
                {
                    "RSTRH", new Dictionary<string, string>() { }
                },
                {
                    "OTH", new Dictionary<string, string>() { }
                },
                {
                    "S0", new Dictionary<string, string>()
                    {
                        { "01100", "S1" },
                        { "00010", "REJ" },
                        { "10010", "RST0S0" }
                    }
                },
                {
                    "REJ", new Dictionary<string, string>() { }
                },
                {
                    "RST0S0", new Dictionary<string, string>() { }
                },
                {
                    "RST0", new Dictionary<string, string>() { }
                },
                {
                    "RSTR", new Dictionary<string, string>() { }
                },
                {
                    "S1", new Dictionary<string, string>()
                    {
                        { "10100", "ESTAB" },
                        { "10010", "RST0" },
                        { "00010", "RSTR" }
                    }
                },
                {
                    "ESTAB", new Dictionary<string, string>()
                    {
                        { "10101", "S2" },
                        { "00101", "S3" }
                    }
                },
                {
                    "S2", new Dictionary<string, string>()
                    {
                        { "00100", "SF" }
                    }
                },
                {
                    "S3", new Dictionary<string, string>()
                    {
                        { "10100", "SF" }
                    }
                },
                {
                    "SF", new Dictionary<string, string>() { }
                }
            };
        }

        private object[] GetContentData(List<Packet> packets)
        {
            int hot = 0;
            int numFailedLogins = 0;
            int loggedIn = 0;
            int numCompromised = 0;
            int rootShell = 0;
            int suAttempted = 0;
            int numRoot = 0;
            int numFileCreations = 0;
            int numAccessFiles = 0;
            int numOutboundCmds = 0;
            int isHotLogin = 0;
            int isGuestLogin = 0;

            for (int i = 0; i < packets.Count; i++)
            {
                TcpPacket tcpPacket = packets[i].Extract<TcpPacket>();
                if (tcpPacket != null)
                {
                    byte[] bytes = tcpPacket.PayloadData;
                    if (bytes != null)
                    {
                        var command = Encoding.ASCII.GetString(bytes);

                        if (loggedIn == 1)
                        {
                            if (command.Contains("#"))
                                rootShell = 1;
                        }   
                        else
                        {
                            if (command == "Last login")
                                loggedIn = 1;
                            if (command == "failed")
                                numFailedLogins += 1;
                        }  
                    }
                }
            }

            return new object[]
            {
                hot, numFailedLogins, loggedIn, numCompromised, rootShell, suAttempted, numRoot,
                numFileCreations, numAccessFiles, numOutboundCmds, isHotLogin, isGuestLogin
            };
        }

        private object[] DeriveHostFeatures(object[] currentConnection, int index, List<object[]> connections, int hosts)
        {
            List<object> services = new List<object>();
            List<object> srvHosts = new List<object>();
            List<object> dstServices = new List<object>();
            List<object> dstSrvHosts = new List<object>();

            int count = 0;
            int serrorCount = 0;
            int rerrorCount = 0;
            int sameSrvCount = 0;
            int diffSrvCount = 0;
            double serrorRate = 0;
            double rerrorRate = 0;
            double sameSrvRate = 0;
            double diffSrvRate = 0;
            int srvCount = 0;
            int srvSerrorCount = 0;
            int srvRerrorCount = 0;
            int srvDiffHostCount = 0;
            double srvSerrorRate = 0;
            double srvRerrorRate = 0;
            double srvDiffHostRate = 0;
            int dstHostCount = 0;
            int dstHostSrvCount = 0;
            int dstHostSerrorCount = 0;
            int dstHostRerrorCount = 0;
            int dstHostSameSrvCount = 0;
            int dstHostDiffSrvCount = 0;
            int dstHostSrvSerrorCount = 0;
            int dstHostSrvRerrorCount = 0;
            int dstHostSrvDiffHostCount = 0;
            double dstHostSameSrvRate = 0;
            double dstHostDiffSrvRate = 0;
            double dstHostSameSrcPortRate = 0;
            double dstHostSrvDiffHostRate = 0;
            double dstHostSerrorRate = 0;
            double dstHostSrvSerrorRate = 0;
            double dstHostRerrorRate = 0;
            double dstHostSrvRerrorRate = 0;

            DateTime endTimestamp = DateTime.Parse(connections[connections.Count - 1][0].ToString());

            for (int i = index; i < index + hosts; i++)
            {
                if (i == connections.Count - 1)
                    break;

                DateTime currentTimestamp = DateTime.Parse(currentConnection[0].ToString());
                if (currentConnection[3].ToString() == connections[i][3].ToString() // dstIp
                    && currentTimestamp >= endTimestamp.AddSeconds(-2)) // timestamp
                {
                    count++;

                    // Count various errors
                    if (currentConnection[10].ToString() != "SF") // statusFlag
                    {
                        if (connections[i][10].ToString().Contains("S"))
                            serrorCount++;
                        else if (connections[i][10].ToString().Contains("R"))
                            rerrorCount++;
                    }

                    // Count the # of same services
                    if (currentConnection[9].ToString() == connections[i][9].ToString()) // service
                        sameSrvCount++;

                    // Count the # of unique (different) services
                    if (count == 1)
                    {
                        services.Add(connections[i][8]); // protocol
                        diffSrvCount++;
                    }
                    else
                    {
                        int j = 0;
                        for (j = 0; j < diffSrvCount; j++)
                            if (services[j].ToString() == connections[i][8].ToString())
                                break;
                        if (j == diffSrvCount)
                        {
                            services.Add(connections[i][8]);
                            diffSrvCount++;
                        }
                    }
                }

                // For the same service
                if (currentConnection[9].ToString() == connections[i][9].ToString() // service
                    && currentTimestamp > endTimestamp.AddSeconds(-2)) // timestamp 
                {
                    srvCount++;

                    // Count various errors
                    if (connections[i][10].ToString() != "SF") // statusFlag
                    {
                        if (connections[i][10].ToString().Contains("S"))
                            srvSerrorCount++;
                        else if (connections[i][10].ToString().Contains("R"))
                            srvRerrorCount++;
                    }

                    // Count the # of unique (different) services
                    if (srvCount == 1)
                    {
                        srvHosts.Add(connections[i][3]); // dstIp
                        srvDiffHostCount++;
                    }
                    else
                    {
                        int j = 0;
                        for (j = 0; j < srvDiffHostCount; j++)
                            if (services[j].ToString() == connections[i][3].ToString()) // dstIp
                                break;
                        if (j == srvDiffHostCount)
                        {
                            services.Add(connections[i][3]); // dstIp
                            srvDiffHostCount++;
                        }
                    }
                }

                // For dstIp
                if (currentConnection[3].ToString() == connections[i][3].ToString()) // dstIp
                {
                    dstHostCount++;

                    // Count various errors
                    if (currentConnection[10].ToString() != "SF") // statusFlag
                    {
                        if (connections[i][10].ToString().Contains("S"))
                            dstHostSerrorCount++;
                        else if (connections[i][10].ToString().Contains("R"))
                            dstHostRerrorCount++;
                    }

                    // Count the # of same services
                    if (currentConnection[9].ToString() == connections[i][9].ToString()) // service
                        dstHostSameSrvCount++;

                    // Count the # of unique (different) services
                    if (dstHostCount == 1)
                    {
                        dstServices.Add(connections[i][8]); // protocol
                        dstHostDiffSrvCount++;
                    }
                    else
                    {
                        int j = 0;
                        for (j = 0; j < dstHostDiffSrvCount; j++)
                            if (dstServices[j].ToString() == connections[i][8].ToString())
                                break;
                        if (j == dstHostDiffSrvCount)
                        {
                            dstServices.Add(connections[i][8]);
                            dstHostDiffSrvCount++;
                        }
                    }
                }

                // For the same service
                if (currentConnection[9].ToString() == connections[i][9].ToString()) // service
                {
                    dstHostSrvCount++;

                    // Count various errors
                    if (connections[i][10].ToString() != "SF") // statusFlag
                    {
                        if (connections[i][10].ToString().Contains("S"))
                            dstHostSrvSerrorCount++;
                        else if (connections[i][10].ToString().Contains("R"))
                            dstHostSrvRerrorCount++;
                    }

                    // Count the # of unique (different) services
                    if (dstHostSrvCount == 1)
                    {
                        dstSrvHosts.Add(connections[i][3]); // dstIp
                        dstHostSrvDiffHostCount++;
                    }
                    else
                    {
                        int j = 0;
                        for (j = 0; j < dstHostSrvDiffHostCount; j++)
                            if (dstSrvHosts[j].ToString() == connections[i][3].ToString()) // dstIp
                                break;
                        if (j == dstHostSrvDiffHostCount)
                        {
                            dstSrvHosts.Add(connections[i][3]); // dstIp
                            dstHostSrvDiffHostCount++;
                        }
                    }
                }
            }

            // End of for loop
            if (count > 0)
            {
                serrorRate = (double)serrorCount / count;
                rerrorRate = (double)rerrorCount / count;

                if (diffSrvCount > 1)
                    diffSrvRate = (double)diffSrvCount / count;
                else
                    diffSrvRate = 0;

                sameSrvRate = (double)sameSrvCount / count;
            }

            if (srvCount > 0)
            {
                srvSerrorRate = (double)srvSerrorCount / srvCount;
                srvRerrorRate = (double)srvRerrorCount / srvCount;

                if (srvDiffHostCount > 1)
                    srvDiffHostRate = (double)srvDiffHostCount / srvCount;
                else
                    srvDiffHostRate = 0;
            }

            if (dstHostCount > 0)
            {
                dstHostSerrorRate = (double)dstHostSerrorCount / dstHostCount;
                dstHostRerrorRate = (double)dstHostRerrorCount / dstHostCount;

                if (dstHostDiffSrvCount > 1)
                    dstHostDiffSrvRate = (double)dstHostDiffSrvCount / dstHostCount;
                else
                    dstHostDiffSrvRate = 0;

                dstHostSameSrvRate = (double)dstHostSameSrvCount / dstHostCount;
            }

            if (srvCount > 0)
            {
                dstHostSrvSerrorRate = (double)dstHostSrvSerrorCount / dstHostSrvCount;
                dstHostSrvRerrorRate = (double)dstHostSrvRerrorCount / dstHostSrvCount;

                if (dstHostSrvDiffHostCount > 1)
                    dstHostSrvDiffHostRate = (double)dstHostSrvDiffHostCount / dstHostSrvCount;
                else
                    dstHostSrvDiffHostRate = 0;
            }

            return new object[]
            {
                count, serrorRate, rerrorRate, sameSrvRate, diffSrvRate,
                srvCount, srvSerrorRate, srvRerrorRate, srvDiffHostRate,
                dstHostCount, dstHostSrvCount, dstHostSameSrvRate, dstHostDiffSrvRate,
                dstHostSameSrcPortRate, dstHostSrvDiffHostRate, dstHostSerrorRate,
                dstHostSrvSerrorRate, dstHostRerrorRate, dstHostSrvRerrorRate
            };
        }
    }
}
