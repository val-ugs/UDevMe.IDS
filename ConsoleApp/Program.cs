using IDS.BusinessLogic.Services;
using IDS.DataAccess.CSV;
using IDS.DataAccess.PCAP;
using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ConsoleApp
{

    public class Program
    {
        public enum ClassifierType
        {
            Knn,
            Mlp,
            RandomForest,
            XGBoost
        }

        public enum Metric
        {
            Accuracy,
            F1Score
        }

        private static readonly string _baseDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string _csvPath = Path.Combine(_baseDirectory, "CsvData");
        private static readonly string _pcapPath = Path.Combine(_baseDirectory, "PcapData");
        private static readonly string _logFilePath = Path.Combine(_baseDirectory, "Logs");
        private static readonly char _delimiter = ',';
        private static DataService _csvDataService;
        private static DataService _pcapDataService;

        private static CaptureFileWriterDevice captureFileWriter;
        private static int _packetIndex_option;

        public static void Main()
        {
            IDS.DataAccess.CSV.DataRepository csvDataRepository = new IDS.DataAccess.CSV.DataRepository(
                    new CsvSettings(_csvPath, _delimiter)
                );
            IDS.DataAccess.PCAP.DataRepository pcapDataRepository =
                new IDS.DataAccess.PCAP.DataRepository(_pcapPath);
            _csvDataService = new DataService(csvDataRepository);
            _pcapDataService = new DataService(pcapDataRepository);

            DisplayHeader();

            bool isCorrectOption = false;

            while (!isCorrectOption)
            {
                Console.WriteLine("Select option:");
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine("1. Detect intrusions in real time");
                Console.WriteLine("2. Verificate algorithms from csv files");
                Console.WriteLine("3. Create capture file from real-time traffic");
                Console.WriteLine("4. Create csv database from multiple pcap files");

                Console.WriteLine();
                Console.Write("-- Please choose option: ");
                Int32.TryParse(Console.ReadLine(), out int option);

                switch (option)
                {
                    case 1:
                        DetectIntrusionsInRealtime();
                        isCorrectOption = true;
                        break;
                    case 2:
                        VerificateAlgorithmsFromCsv();
                        isCorrectOption = true;
                        break;
                    case 3:
                        CreateCaptureFileFromRealTimeTraffic();
                        isCorrectOption = true;
                        break;
                    case 4:
                        CreateCsvFromPcapFiles();
                        isCorrectOption = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private static void DetectIntrusionsInRealtime()
        {
            // Prepare
            string pcapFilename = "AUTO";
            int captureTime = 30000; // In milliseconds (1 minute = 60000 milliseconds)
            string[] ListOfCsvFilenames = _csvDataService.GetFilenameList();

            if (ListOfCsvFilenames.Length == 0)
            {
                Console.WriteLine("Csv files are not found");
                return;
            }

            string trainFilename = GetFilename(ListOfCsvFilenames, isTrain: true);
            List<string[]> trainData = GetDataFromCsvFile(trainFilename);

            TrafficDataConverterService trafficDataConverterService = GetTrafficDataConverterService(DefineDataSource(trainFilename));

            TrafficData trainTrafficData = trafficDataConverterService.ConvertTrainData(trainData);

            int trainNumberOfSamples = TryReadValueFromConsole("\nEnter train number of samples",
                                                           min: 1, max: trainTrafficData.Samples.Count);
            trainTrafficData.Samples = trainTrafficData.Samples.Take(trainNumberOfSamples).ToList();

            NormalizeFeaturesService normalizeFeaturesService = GetNormalizeFeaturesService();
            if (normalizeFeaturesService != null)
                trainTrafficData.Samples = normalizeFeaturesService.NormalizeTrainSamples(trainTrafficData.Samples);

            IClassifierService classifierService = GetClassifier();
            classifierService.Train(trainTrafficData);

            LibPcapLiveDevice selectedDevice = SelectDevice();

            string ext = (pcapFilename.EndsWith(".pcap") || pcapFilename.EndsWith(".pcapng")) ? "" : ".pcap";
            pcapFilename = DataSource.RealTime.ToString().ToUpper() + "_" + pcapFilename + ext;

            string logFilename = "log.log";
            StringBuilder log = new StringBuilder();

            //RUN
            Console.WriteLine("Press Enter to stop...");
            do
            {
                while (!Console.KeyAvailable)
                {
                    CreateCaptureFileFromRealtimeTraffic(pcapFilename, selectedDevice, captureTime);

                    List<string[]> testData = _pcapDataService.GetData(pcapFilename);

                    if(testData.Count > 0)
                    {
                        TrafficData testTrafficData = trafficDataConverterService.ConvertTestData(testData, hasLabel: false);

                        if (normalizeFeaturesService != null)
                            testTrafficData.Samples = normalizeFeaturesService.NormalizeTestSamples(testTrafficData.Samples);

                        List<int> labels = classifierService.Predict(testTrafficData);
                        List<string> labelNames = labels.Select(l => trafficDataConverterService.GetNameByLabel(l)).ToList();

                        var statistics = labelNames.GroupBy(l => l)
                                    .Select(x => new
                                    {
                                        LabelName = x.Key,
                                        Count = x.Count()
                                    });

                        log.Append(DateTime.Now.ToString());
                        foreach (var statistic in statistics)
                        {
                            log.Append(String.Format(" Label Name: {0} - Count: {1};", statistic.LabelName, statistic.Count));
                        }
                        log.Append(Environment.NewLine);
                        File.AppendAllText(Path.Combine(_logFilePath, logFilename), log.ToString());
                        log.Clear();
                    }
                }
            } while (Console.ReadKey(true).Key != ConsoleKey.Enter);
        }

        private static void VerificateAlgorithmsFromCsv()
        {
            Stopwatch stopWatch = new Stopwatch();

            string[] ListOfCsvFilenames = _csvDataService.GetFilenameList();

            if (ListOfCsvFilenames.Length == 0)
            {
                Console.WriteLine("Csv files are not found");
                return;
            }

            string trainFilename = GetFilename(ListOfCsvFilenames, isTrain: true);
            List<string[]> trainData = GetDataFromCsvFile(trainFilename);
            string testFilename = GetFilename(ListOfCsvFilenames, isTrain: true);
            List<string[]> testData = GetDataFromCsvFile(testFilename);

            stopWatch.Start();
            TrafficDataConverterService trafficDataConverterService = GetTrafficDataConverterService(DefineDataSource(trainFilename));
            TrafficData trainTrafficData = trafficDataConverterService.ConvertTrainData(trainData);
            TrafficData testTrafficData = trafficDataConverterService.ConvertTestData(testData, hasLabel: true);
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}\n", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("\nData conversion time: " + elapsedTime);

            int trainNumberOfSamples = TryReadValueFromConsole("Enter train number of samples",
                                                           min: 1, max: trainTrafficData.Samples.Count);
            int testNumberOfSamples = TryReadValueFromConsole("\nEnter test number of samples",
                                                          min: 1, max: testTrafficData.Samples.Count);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(trainNumberOfSamples).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(testNumberOfSamples).ToList();
            List<int> trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            stopWatch.Restart();
            NormalizeFeaturesService normalizeFeaturesService = GetNormalizeFeaturesService();
            trainTrafficData.Samples = normalizeFeaturesService.NormalizeTrainSamples(trainTrafficData.Samples);
            testTrafficData.Samples = normalizeFeaturesService.NormalizeTestSamples(testTrafficData.Samples);
            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("\nData normalization time: " + elapsedTime);

            IClassifierService classifierService = GetClassifier();
            
            stopWatch.Restart();
            Console.WriteLine("Training...");
            classifierService.Train(trainTrafficData);
            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}\n", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("\nTraining time: " + elapsedTime);

            stopWatch.Restart();
            Console.WriteLine("Predicting...");
            List<int> result = classifierService.Predict(testTrafficData);
            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("\nPrediction time: " + elapsedTime);

            int metricNumber;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Select metric:");
                int i = 1;
                foreach (Metric metricName in Enum.GetValues(typeof(Metric)))
                    Console.WriteLine(i++ + ": " + metricName);
                Console.WriteLine();
                Console.Write("-- Please choose metric (number): ");
                if (Int32.TryParse(Console.ReadLine(), out metricNumber))
                    if (metricNumber > 0 && metricNumber <= i)
                        break;
            }

            Metric metric = (Metric)Enum.ToObject(typeof(Metric), metricNumber - 1);
            double accuracy = CalculateAccuracyByMetric(metric, trueLabels, result);

            Console.WriteLine();
            Console.WriteLine("Accuracy: {0}", accuracy);
            Console.ReadLine();
        }

        private static string GetFilename(string[] ListOfFilenames, bool isTrain)
        {
            int fileNumber;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Select {0} file:", isTrain ? "Train" : "Test");
                for (int i = 1; i <= ListOfFilenames.Length; i++)
                    Console.WriteLine(i + ": " + ListOfFilenames[i - 1]);
                Console.WriteLine();
                Console.Write("-- Please choose file (number): ");
                if (Int32.TryParse(Console.ReadLine(), out fileNumber))
                    if (fileNumber > 0 && fileNumber <= ListOfFilenames.Length)
                        break;
            }

            return ListOfFilenames[fileNumber - 1];
        }

        private static List<string[]> GetDataFromCsvFile(string filename)
        {
            bool isHeaderRow;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("File has header row? (yes, no)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                {
                    if (text.ToUpper() == "yes".ToUpper())
                        isHeaderRow = true;
                    else
                        isHeaderRow = false;
                    break;
                }
            }

            return _csvDataService.GetData(filename, isHeaderRow);
        }

        private static TrafficDataConverterService GetTrafficDataConverterService(DataSource dataSource)
        {
            ClassificationType classificationType;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Binary or multiclass? (binary, multiclass)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "binary".ToUpper() || text.ToUpper() == "multiclass".ToUpper())
                {
                    if (text.ToUpper() == "binary".ToUpper())
                        classificationType = ClassificationType.Binary;
                    else
                        classificationType = ClassificationType.Multiclass;
                    break;
                }
            }

            bool hasOneHotEncode;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Encode categorical data using OneHotEncode or Skip (OneHotEncode, Skip)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "OneHotEncode".ToUpper() || text.ToUpper() == "Skip".ToUpper())
                {
                    if (text.ToUpper() == "OneHotEncode".ToUpper())
                        hasOneHotEncode = true;
                    else
                        hasOneHotEncode = false;
                    break;
                }
            }

            return new TrafficDataConverterService(dataSource, classificationType, hasOneHotEncode);
        }

        private static NormalizeFeaturesService GetNormalizeFeaturesService()
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Normalize? (yes, no)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                {
                    if (text.ToUpper() == "yes".ToUpper())
                        return new NormalizeFeaturesService(0, 1);
                    else
                        break;
                }
            }

            return null;
        }

        private static DataSource DefineDataSource(string filename)
        {
            if (filename.ToUpper().StartsWith(DataSource.RealTime.ToString().ToUpper()))
                return DataSource.RealTime;
            if (filename.ToUpper().StartsWith(DataSource.Unsw.ToString().ToUpper()))
                return DataSource.Unsw;
            if (filename.ToUpper().StartsWith(DataSource.Kdd.ToString().ToUpper()))
                return DataSource.Kdd;
            throw new Exception("Not find data source");
        }

        private static IClassifierService GetClassifier()
        {
            int classifierNumber;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Select classifier:");
                int i = 1;
                foreach (ClassifierType classifierName in Enum.GetValues(typeof(ClassifierType)))
                    Console.WriteLine(i++ + ": " + classifierName);
                Console.WriteLine();
                Console.Write("-- Please choose classifier (number): ");
                if (Int32.TryParse(Console.ReadLine(), out classifierNumber))
                    if (classifierNumber > 0 && classifierNumber <= i)
                        break;
            }

            ClassifierType classifierType = (ClassifierType)Enum.ToObject(typeof(ClassifierType), classifierNumber - 1);

            switch (classifierType)
            {
                case ClassifierType.Knn:
                    return KnnClassifier();
                case ClassifierType.Mlp:
                    return MlpClassifier();
                case ClassifierType.RandomForest:
                    return RandomForestClassifier();
                case ClassifierType.XGBoost:
                    return XGBoostClassifier();
            }

            return null;
        }

        private static IClassifierService KnnClassifier()
        {
            int numberOfNeighbors = 3;
            numberOfNeighbors = TryReadValueFromConsole("Enter number of neighbors", numberOfNeighbors);

            Console.WriteLine("Initialization Knn...");
            return new KnnService(numberOfNeighbors);
        }

        private static IClassifierService MlpClassifier()
        {
            int numberOfLayers = 2;
            bool defaulthiddenLayers = false;
            while (true)
            {
                Console.WriteLine("Enter number of hidden layers (Default: {0} hidden layers - (100, 50)):", numberOfLayers);
                string text = Console.ReadLine();
                if (text == "")
                {
                    defaulthiddenLayers = true;
                    break;
                }
                if (Int32.TryParse(text, out int value))
                    if (value > 0)
                    {
                        numberOfLayers = value;
                        break;
                    }
            }
            List<int> hiddenLayersWithNeurons;
            if (defaulthiddenLayers == false)
            {
                hiddenLayersWithNeurons = new List<int>(numberOfLayers);
                int i = 1;
                while (hiddenLayersWithNeurons.Count != numberOfLayers)
                {
                    Console.WriteLine("Enter number of Neurons in the {0} hidden layer:", i);
                    if (Int32.TryParse(Console.ReadLine(), out int numberOfNeurons))
                        if (numberOfNeurons > 0)
                        {
                            hiddenLayersWithNeurons.Add(numberOfNeurons);
                            i++;
                        }
                }
            }
            else
            {
                hiddenLayersWithNeurons = new List<int> { 100, 50 };
            }

            double alpha = 0.0001;
            alpha = TryReadValueFromConsole("Enter alpha", alpha);

            int batchSize = 200;
            batchSize = TryReadValueFromConsole("Enter batch size", batchSize);

            double learningRate = 0.001;
            learningRate = TryReadValueFromConsole("Enter learning rate", learningRate);

            int maxIterations = 1000;
            maxIterations = TryReadValueFromConsole("Enter max iterations", maxIterations);

            double tol = 0.0001;
            tol = TryReadValueFromConsole("Enter tol", tol);

            double beta_1 = 0.9;
            beta_1 = TryReadValueFromConsole("Enter beta_1", beta_1);

            double beta_2 = 0.999;
            beta_2 = TryReadValueFromConsole("Enter beta_2", beta_2);

            double epsilon = 0.00000001;
            epsilon = TryReadValueFromConsole("Enter epsilon", epsilon);

            Console.WriteLine("Initialization multilayer perceptron...");
            return new MlpService(hiddenLayersWithNeurons, alpha, batchSize, learningRate, maxIterations, tol, beta_1, beta_2, epsilon);
        }
        private static IClassifierService RandomForestClassifier()
        {
            int numberOfTrees = 3;
            numberOfTrees = TryReadValueFromConsole("Enter num Trees", numberOfTrees);

            int maxDepth = 5;
            maxDepth = TryReadValueFromConsole("Enter max depth", maxDepth);

            int minSize = 3;
            minSize = TryReadValueFromConsole("Enter min size", minSize);

            double partOfTrafficDataRatio = 0.5;
            partOfTrafficDataRatio = TryReadValueFromConsole("Enter part of traffic data ratio", partOfTrafficDataRatio);

            Console.WriteLine("Initialization random forest...");
            return new RandomForestService(numberOfTrees, maxDepth, minSize, partOfTrafficDataRatio);
        }
        private static IClassifierService XGBoostClassifier()
        {
            int rounds = 5;
            rounds = TryReadValueFromConsole("Enter rounds", rounds);

            int maxDepth = 10;
            maxDepth = TryReadValueFromConsole("Enter max depth", maxDepth);

            int minSize = 3;
            minSize = TryReadValueFromConsole("Enter min size", minSize);

            double learningRate = 0.4;
            learningRate = TryReadValueFromConsole("Enter learning rate", learningRate);

            double lambda = 1.5;
            lambda = TryReadValueFromConsole("Enter lambda", lambda);

            int gamma = 1;
            gamma = TryReadValueFromConsole("Enter gamma", gamma);

            double nFeatureRatio = 0.8;
            nFeatureRatio = TryReadValueFromConsole("Enter n feature ratio", nFeatureRatio);

            Console.WriteLine("Initialization XGBoost...");
            return new XGBoostService(rounds, maxDepth, minSize, learningRate, lambda, gamma, nFeatureRatio);
        }

        private static double CalculateAccuracyByMetric(Metric metric, List<int> trueLabels, List<int> predictedLabels)
        {
            switch (metric)
            {
                case Metric.Accuracy:
                    AccuracyMetricService accuracyMetricService = new AccuracyMetricService();
                    return accuracyMetricService.Calculate(trueLabels, predictedLabels);
                case Metric.F1Score:
                    F1ScoreMetricService f1ScoreMetricService = new F1ScoreMetricService();
                    return f1ScoreMetricService.Calculate(trueLabels, predictedLabels);
            }

            return 0;
        }

        private static void CreateCaptureFileFromRealTimeTraffic()
        {
            Console.Write("-- Please enter the output file name: ");
            string filename = Console.ReadLine();
            LibPcapLiveDevice selectedDevice = SelectDevice();

            string ext = (filename.EndsWith(".pcap") || filename.EndsWith(".pcapng")) ? "" : ".pcap";
            filename = DataSource.RealTime.ToString().ToUpper() + "_" + filename + ext;

            CreateCaptureFileFromRealtimeTraffic(filename, selectedDevice, 0);
        }

        private static LibPcapLiveDevice SelectDevice()
        {
            // Retrieve the device list
            var devices = LibPcapLiveDeviceList.Instance;

            // If no devices were found print an error
            if (devices.Count < 1)
            {
                Console.WriteLine("No devices were found on this machine");
                return null;
            }

            Console.WriteLine();
            Console.WriteLine("The following devices are available on this machine:");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine();

            int deviceNumber;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Select device:");
                int i = 1;

                // Print out the devices
                foreach (var dev in devices)
                {
                    /* Description */
                    Console.WriteLine("{0}) {1} {2}", i, dev.Name, dev.Description);
                    i++;
                }

                Console.WriteLine();
                Console.Write("-- Please choose a device to capture on: ");
                if (Int32.TryParse(Console.ReadLine(), out deviceNumber))
                    if (deviceNumber > 0 && deviceNumber <= i)
                        break;
            }

            return devices[deviceNumber];
        }

        private static void CreateCaptureFileFromRealtimeTraffic(string filename, LibPcapLiveDevice selectedDevice, int captureTime)
        {
            _packetIndex_option = 0;
            using var device = selectedDevice;

            // Register our handler function to the 'packet arrival' event
            device.OnPacketArrival +=
                new PacketArrivalEventHandler(device_OnPacketArrival_Option);

            // Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(mode: DeviceModes.Promiscuous | DeviceModes.DataTransferUdp | DeviceModes.NoCaptureLocal, read_timeout: readTimeoutMilliseconds);

            if (captureTime == 0)
            {
                Console.WriteLine();
                Console.WriteLine("-- Listening on {0} {1}, writing to {2}, hit 'Enter' to stop...",
                                  device.Name, device.Description,
                                  filename);
            }
                

            // open the output file
            captureFileWriter = new CaptureFileWriterDevice(Path.Combine(_pcapPath, filename));
            captureFileWriter.Open(device);

            // Start the capturing process
            device.StartCapture();

            if (captureTime > 0)
                Thread.Sleep(captureTime);
            else
                Console.ReadLine(); // Wait for 'Enter' from the user.

            // Stop the capturing process
            device.StopCapture();

            Console.WriteLine("-- Capture stopped.");

            // Print out the device statistics
            Console.WriteLine(device.Statistics.ToString());
        }

        /// <summary>
        /// // Write the packet to the file
        /// </summary>
        private static void device_OnPacketArrival_Option(object sender, PacketCapture e)
        {
            //var device = (ICaptureDevice)sender;

            // Write the packet to the file
            var rawPacket = e.GetPacket();
            captureFileWriter.Write(rawPacket);
            Console.WriteLine("Packet dumped to file.");

            if (rawPacket.LinkLayerType == LinkLayers.Ethernet)
            {
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                var ethernetPacket = (EthernetPacket)packet;

                Console.WriteLine("{0} At: {1}:{2}: MAC:{3} -> MAC:{4}",
                                  _packetIndex_option,
                                  rawPacket.Timeval.Date.ToString(),
                                  rawPacket.Timeval.Date.Millisecond,
                                  ethernetPacket.SourceHardwareAddress,
                                  ethernetPacket.DestinationHardwareAddress);
                _packetIndex_option++;
            }
        }

        private static void CreateCsvFromPcapFiles()
        {
            string[] pcapFilenames = _pcapDataService.GetFilenameList();
            List<string> selectedPcapFilenames = new List<string>();
            List<string[]> newData = new List<string[]>();

            pcapFilenames = pcapFilenames.Where(f => f.ToUpper().StartsWith(DataSource.RealTime.ToString().ToUpper())).ToArray();
            if (pcapFilenames.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("{0} files are not found", DataSource.RealTime.ToString().ToUpper());
                return;
            }

            Console.WriteLine();
            int fileCount = TryReadValueFromConsole("How many pcap files to convert to csv? If there are several, then merge into one?",
                                                    min: 1, max: pcapFilenames.Length);

            while (selectedPcapFilenames.Count != fileCount)
            {
                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("Select file:");
                    for (int i = 1; i <= pcapFilenames.Length; i++)
                        Console.WriteLine(i + ": " + pcapFilenames[i - 1]);
                    Console.WriteLine();
                    Console.Write("-- Please choose file (number): ");
                    if (Int32.TryParse(Console.ReadLine(), out int fileNumber))
                        if (fileNumber > 0 && fileNumber <= pcapFilenames.Length)
                            if (selectedPcapFilenames.Contains(pcapFilenames[fileNumber - 1]))
                                Console.WriteLine("This file is selected");
                            else
                            {
                                selectedPcapFilenames.Add(pcapFilenames[fileNumber - 1]);
                                break;
                            }
                }
            }

            bool hasLabels;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Has labels? (yes, no)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                {
                    if (text.ToUpper() == "yes".ToUpper())
                        hasLabels = true;
                    else
                        hasLabels = false;
                    break;
                }
            }

            foreach (string pcapFilename in selectedPcapFilenames)
            {
                Console.WriteLine();
                Console.WriteLine("Reading data from {0}...", pcapFilename);
                List<string[]> data = _pcapDataService.GetData(pcapFilename);
                List<string[]> partOfData = new List<string[]>();

                int dataCount = 0;
                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("How many packets get? (1-{0})", data.Count);
                    if (Int32.TryParse(Console.ReadLine(), out dataCount))
                        if (dataCount > 0 && dataCount <= data.Count)
                            break;
                }

                bool isRandom = false;
                if (dataCount != data.Count)
                {
                    while (true)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Take the first packets or random? (first, random)");
                        string text = Console.ReadLine();
                        if (text.ToUpper() == "first".ToUpper() || text.ToUpper() == "random".ToUpper())
                        {
                            if (text.ToUpper() == "first".ToUpper())
                                isRandom = false;
                            else
                                isRandom = true;
                            break;
                        }
                    }
                }

                string label = "Normal";
                if (hasLabels)
                {
                    Console.WriteLine();
                    Console.WriteLine("What label? (Enter normal or name of attack)");
                    label = Console.ReadLine();
                }

                int index = 0;
                Random rand = new Random();
                while (partOfData.Count != dataCount)
                {
                    if (isRandom)
                        index = rand.Next(0, data.Count);

                    if (hasLabels)
                    {
                        List<string> dataRow = data[index].ToList();
                        dataRow.Add(label.ToUpper());
                        partOfData.Add(dataRow.ToArray());
                    }
                    else
                        partOfData.Add(data[index]);


                    data.RemoveAt(index);
                }

                newData.AddRange(partOfData);
            }

            bool canShuffle;
            while (true)
            {
                Console.WriteLine("Shuffle? (yes, no)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                {
                    if (text.ToUpper() == "yes".ToUpper())
                        canShuffle = true;
                    else
                        canShuffle = false;
                    break;
                }
            }
            if (canShuffle)
            {
                Console.WriteLine("Shuffling data...");
                var sorted = newData.OrderBy(a => Guid.NewGuid()).ToList();
                newData.Clear();
                newData.AddRange(sorted);
            }

            // Create Csv File
            Console.WriteLine("Enter file name for new csv file?");
            string newFilename = Console.ReadLine();
            string ext = newFilename.EndsWith(".csv") ? "" : ".csv";
            string fullPath = Path.Combine(_csvPath, DataSource.RealTime.ToString().ToUpper() + "_" + newFilename + ext);

            using (var stream = File.Create(fullPath))
            {
                using (var writer = new StreamWriter(stream))
                {
                    foreach (string[] dataRow in newData)
                    {
                        StringBuilder dataRowInCsv = new StringBuilder();

                        for (int j = 0; j < dataRow.Length; j++)
                        {
                            string dataMember = Convert.ToString(dataRow[j]);
                            dataMember = dataMember.Replace(',', '.'); // if dataMember is double
                            if (j == dataRow.Length - 1)
                                dataRowInCsv.Append($"{dataMember}");
                            else
                                dataRowInCsv.Append($"{dataMember},");
                        }

                        writer.WriteLine(dataRowInCsv);
                    }
                    writer.Close();
                }
            }
        }

        private static int TryReadValueFromConsole(string question, int value)
        {
            while (true)
            {
                Console.WriteLine(question + " (Default: {0}):", value);
                string text = Console.ReadLine();
                if (text == "")
                    break;
                if (Int32.TryParse(text, out int parseValue))
                    if (value > 0)
                    {
                        value = parseValue;
                        break;
                    }
            }

            return value;
        }

        private static double TryReadValueFromConsole(string question, double value)
        {
            while (true)
            {
                Console.WriteLine(question + " (Default: {0}):", value);
                string text = Console.ReadLine();
                if (text == "")
                    break;
                if (Double.TryParse(text, out double parseValue))
                    if (value > 0)
                    {
                        value = parseValue;
                        break;
                    }
            }

            return value;
        }

        private static int TryReadValueFromConsole(string question, int min, int max)
        {
            int value;
            while (true)
            {
                Console.WriteLine(question + " (min:{0}, max:{1})", min, max);
                Console.Write("-- Number of files: ");
                if (Int32.TryParse(Console.ReadLine(), out value))
                    if (value >= min && value <= max)
                        break;
            }

            return value;
        }

        private static void DisplayHeader()
        {
            var logo = new[]
            {
                    @"@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@",
                    @"@  _ _      _ _   _ _......                _ _      _ _   _ _  _ _  _ _              @",
                    @"@ ( V )    ( v ) ( v ..... \              ( V )    ( v ) ( v '' v '' v )             @",
                    @"@  | |      | |   | |     \ |  _ _..._ _   | |      | |   | /''\ /''\ |   _ _..._ _  @",
                    @"@  | |      | |   | |     | | ( v ... v )  | |      | |   | |  | |  | |  ( v ... v ) @",
                    @"@  | |      | |   | |     | |  | |   | |   | |      | |   | |  | |  | |   | |   | |  @",
                    @"@  | |      | |   | |     | |  |  '''_^_)  | |      | |   | |  | |  | |   |  '''_^_) @",
                    @"@  | |      | |   | |     / |  | ('''  _    \ \    / /    | |  | |  | |   | ('''  _  @",
                    @"@ (_^_'....'_^_) (_^_'''''./  (_^_'...'_)    \ '..' /     | |  | |  | |  (_^_'...'_) @",
                    @"@     '....'         '''''        '''''       '....'     (_^_)(_^_)(_^/      '''''   @",
                    @"@                                                                                    @",
                    @"@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@"
            };
            var title = new[]
            {
                    @"                           @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@",
                    @"                           @  _ _   _ _......             @",
                    @"                           @ ( V ) ( v ..... \   /'''''.  @",
                    @"                           @  | |   | |     \ | / /'''._) @",
                    @"                           @  | |   | |     | | \ \       @",
                    @"                           @  | |   | |     | |  \ ''''\  @",
                    @"                           @  | |   | |     | |   ''''\ \ @",
                    @"                           @  | |   | |     / |  _    / / @",
                    @"                           @ (_^_) (_^_'''''./  ( '''' /  @",
                    @"                           @           '''''     ''''''   @",
                    @"                           @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@"
            };
            var author =
                    @"                                                          Created By Valentin Charugin";

            Console.ForegroundColor = ConsoleColor.Gray;
            foreach (string line in logo)
                Console.WriteLine(line);
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (string line in title)
                Console.WriteLine(line);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(author);
            Console.ResetColor();
        }
    }
}
