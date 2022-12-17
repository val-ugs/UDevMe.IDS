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
using System.Text.Json;
using System.Threading;

namespace ConsoleApp
{
    public class Config
    {
        public int CaptureTime { get; set; }
        public string TrainFilename { get; set; }
        public bool HasHeaderRow { get; set; }
        public int TrainNumberOfSamples { get; set; }
        public ClassificationType ClassificationType { get; set; }
        public bool UseOneHotEncode { get; set; }
        public int ClassifierNumber { get; set; }
        public ClassifierData ClassifierData { get; set; }
        public int DeviceNumber { get; set; }
    }

    public class ClassifierData
    {
        public KnnData KnnData { get; set; }
        public MlpData MlpData { get; set; }
        public RandomForestData RandomForestData { get; set; }
        public XGBoostData XGBoostData { get; set; }
    }

    public class KnnData
    {
        public int NumberOfNeighbors { get; set; }
    }

    public class MlpData
    {
        public List<int> HiddenLayersWithNeurons { get; set; }
        public double Alpha { get; set; }
        public int BatchSize { get; set; }
        public double LearningRate { get; set; }
        public int MaxIterations { get; set; }
        public double Tol { get; set; }
        public double Beta_1 { get; set; }
        public double Beta_2 { get; set; }
        public double Epsilon { get; set; }
    }

    public class RandomForestData
    {
        public int NumberOfTrees { get; set; }
        public int MaxDepth { get; set; }
        public int MinSize { get; set; }
        public double PartOfTrafficDataRatio { get; set; }
    }

    public class XGBoostData
    {
        public int Rounds { get; set; }
        public int MaxDepth { get; set; }
        public int MinSize { get; set; }
        public double LearningRate { get; set; }
        public double Lambda { get; set; }
        public int Gamma { get; set; }
        public double NFeatureRatio { get; set; }
    }

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
        private static readonly string _configPath = Path.Combine(_baseDirectory, "Configs");
        private static readonly string _logFilePath = Path.Combine(_baseDirectory, "Logs");
        private static readonly char _delimiter = ',';
        private static DataService _csvDataService;
        private static DataService _pcapDataService;

        private static string _reservedPcapFilename = "AUTO";

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
                Console.WriteLine("1. Detect intrusions in real time (use config file)");
                Console.WriteLine("2. Detect intrusions in real time (console input)");
                Console.WriteLine("3. Create config file");
                Console.WriteLine("4. Verificate algorithms from csv files");
                Console.WriteLine("5. Create capture file from real-time traffic");
                Console.WriteLine("6. Create csv database from multiple pcap files");

                Console.WriteLine();
                Console.Write("-- Please choose option: ");
                Int32.TryParse(Console.ReadLine(), out int option);

                switch (option)
                {
                    case 1:
                        DetectIntrusionsInRealtimeWithConfigFile();
                        isCorrectOption = true;
                        break;
                    case 2:
                        DetectIntrusionsInRealtimeWithConsoleInput();
                        isCorrectOption = true;
                        break;
                    case 3:
                        CreateConfigFile();
                        isCorrectOption = true;
                        break;
                    case 4:
                        VerificateAlgorithmsFromCsv();
                        isCorrectOption = true;
                        break;
                    case 5:
                        CreateCaptureFileFromRealTimeTraffic();
                        isCorrectOption = true;
                        break;
                    case 6:
                        CreateCsvFromPcapFiles();
                        isCorrectOption = true;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// <para> Detect intrusions in real-time with config file </para>
        /// </summary>
        private static void DetectIntrusionsInRealtimeWithConfigFile()
        {
            string[] ListOfFilenames = Directory.GetFiles(_configPath).Select(file => Path.GetFileName(file)).ToArray();
            if (ListOfFilenames.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Config files not found");
                return;
            }

            string filename = GetConfigFilename(ListOfFilenames);
            string fullpath = Path.Combine(_configPath, filename);
            Config config;
            using (StreamReader r = new StreamReader(fullpath))
            {
                string json = r.ReadToEnd();
                config = JsonSerializer.Deserialize<Config>(json);
            }

            Console.WriteLine("Initialization...");

            List<string[]> trainData =  GetDataFromCsvFile(config.TrainFilename, config.HasHeaderRow);

            if (trainData == null)
            {
                Console.WriteLine();
                Console.WriteLine("Training file not found");
                return;
            }

            if (config.TrainNumberOfSamples < 0 || config.TrainNumberOfSamples > trainData.Count())
            {
                Console.WriteLine();
                Console.WriteLine("Wrong number of training file samples");
                return;
            }

            TrafficDataConverterService trafficDataConverterService = GetTrafficDataConverterService(DefineDataSource(config.TrainFilename), config.ClassificationType, config.UseOneHotEncode);

            TrafficData trainTrafficData = trafficDataConverterService.ConvertTrainData(trainData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(config.TrainNumberOfSamples).ToList();

            NormalizeFeaturesService normalizeFeaturesService = new NormalizeFeaturesService(0, 1);
            trainTrafficData.Samples = normalizeFeaturesService.NormalizeTrainSamples(trainTrafficData.Samples);

            IClassifierService classifierService = GetClassifier(config.ClassifierNumber, config.ClassifierData);
            classifierService.Train(trainTrafficData);

            // Retrieve the device list
            var devices = LibPcapLiveDeviceList.Instance;
            if (devices.Count < 1 || devices.Count < config.DeviceNumber)
            {
                Console.WriteLine("No devices were found on this machine");
                return;
            }
            LibPcapLiveDevice selectedDevice = devices[config.DeviceNumber];

            string ext = (_reservedPcapFilename.EndsWith(".pcap") || _reservedPcapFilename.EndsWith(".pcapng")) ? "" : ".pcap";
            string pcapFilename = DataSource.RealTime.ToString().ToUpper() + "_" + _reservedPcapFilename + ext;

            string logFilename = "log.log";
            StringBuilder log = new StringBuilder();

            //RUN
            Console.WriteLine("Press Enter to stop...");
            do
            {
                while (!Console.KeyAvailable)
                {
                    CreateCaptureFileFromRealtimeTraffic(pcapFilename, selectedDevice, config.CaptureTime);

                    List<string[]> testData = _pcapDataService.GetData(pcapFilename);

                    if (testData.Count > 0)
                    {
                        TrafficData testTrafficData = trafficDataConverterService.ConvertTestData(testData, hasLabel: false);

                        testTrafficData.Samples = normalizeFeaturesService.NormalizeTestSamples(testTrafficData.Samples);

                        List<int> labels = classifierService.Predict(testTrafficData);
                        List<string> labelNames = labels.Select(l => trafficDataConverterService.GetNameByLabel(l)).ToList();

                        var statistics = labelNames.GroupBy(l => l)
                                    .Select(x => new
                                    {
                                        LabelName = x.Key,
                                        Count = x.Count()
                                    });

                        log.Append(DateTime.Now.ToString("MMM dd HH:mm:ss ", System.Globalization.CultureInfo.CreateSpecificCulture("en-US")));
                        log.Append("hostname IDS:");
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

        /// <summary>
        /// <para> Get config filename </para>
        /// </summary>
        /// <param name="ListOfFilenames"> list of filenames </param>
        /// <returns> config filename </returns>
        private static string GetConfigFilename(string[] ListOfFilenames)
        {
            int fileNumber;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Select Config file:");
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

        /// <summary>
        /// <para> Detect intrusions in real-time with console input</para>
        /// </summary>
        private static void DetectIntrusionsInRealtimeWithConsoleInput()
        {
            // Prepare
            Console.WriteLine();
            int captureTime = 30000; // In milliseconds (1 minute = 60000 milliseconds)
            captureTime = TryReadValueFromConsole("Enter capture time (in milliseconds, 1 minute = 60000 milliseconds)", captureTime);

            string[] ListOfCsvFilenames = _csvDataService.GetFilenameList();
            ListOfCsvFilenames = ListOfCsvFilenames.Where(f => f.StartsWith(DataSource.RealTime.ToString().ToUpper())).ToArray();

            if (ListOfCsvFilenames.Length == 0)
            {
                Console.WriteLine("Csv files are not found");
                return;
            }

            string trainFilename = GetFilename(ListOfCsvFilenames, isTrain: true);
            List<string[]> trainData = GetDataFromCsvFile(trainFilename);

            int trainNumberOfSamples = TryReadValueFromConsole("\nEnter train number of samples",
                                                           min: 1, max: trainData.Count());

            TrafficDataConverterService trafficDataConverterService = GetTrafficDataConverterService(DefineDataSource(trainFilename));

            TrafficData trainTrafficData = trafficDataConverterService.ConvertTrainData(trainData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(trainNumberOfSamples).ToList();

            NormalizeFeaturesService normalizeFeaturesService = new NormalizeFeaturesService(0, 1);
            trainTrafficData.Samples = normalizeFeaturesService.NormalizeTrainSamples(trainTrafficData.Samples);

            IClassifierService classifierService = GetClassifier();
            classifierService.Train(trainTrafficData);

            LibPcapLiveDevice selectedDevice = SelectDevice();

            string ext = (_reservedPcapFilename.EndsWith(".pcap") || _reservedPcapFilename.EndsWith(".pcapng")) ? "" : ".pcap";
            string pcapFilename = DataSource.RealTime.ToString().ToUpper() + "_" + _reservedPcapFilename + ext;

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

                        testTrafficData.Samples = normalizeFeaturesService.NormalizeTestSamples(testTrafficData.Samples);

                        List<int> labels = classifierService.Predict(testTrafficData);
                        List<string> labelNames = labels.Select(l => trafficDataConverterService.GetNameByLabel(l)).ToList();

                        var statistics = labelNames.GroupBy(l => l)
                                    .Select(x => new
                                    {
                                        LabelName = x.Key,
                                        Count = x.Count()
                                    });

                        log.Append(DateTime.Now.ToString("MMM dd HH:mm:ss ", System.Globalization.CultureInfo.CreateSpecificCulture("en-US")));
                        log.Append("hostname IDS:");
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

        /// <summary>
        /// <para> Create config file </para>
        /// </summary>
        private static void CreateConfigFile()
        {
            Config config = new Config();

            Console.WriteLine();
            config.CaptureTime = 30000; // In milliseconds (1 minute = 60000 milliseconds)
            config.CaptureTime = TryReadValueFromConsole("Enter capture time (in milliseconds, 1 minute = 60000 milliseconds)", config.CaptureTime);

            string[] ListOfCsvFilenames = _csvDataService.GetFilenameList();
            ListOfCsvFilenames = ListOfCsvFilenames.Where(f => f.StartsWith(DataSource.RealTime.ToString().ToUpper())).ToArray();

            if (ListOfCsvFilenames.Length == 0)
            {
                Console.WriteLine("Csv files are not found");
                return;
            }
            config.TrainFilename = GetFilename(ListOfCsvFilenames, isTrain: true);

            config.HasHeaderRow = GetHasHeaderRowFromConsole();

            List<string[]> trainData = _csvDataService.GetData(config.TrainFilename, config.HasHeaderRow); ;
            config.TrainNumberOfSamples = TryReadValueFromConsole("\nEnter train number of samples",
                                                           min: 1, max: trainData.Count());

            config.ClassificationType = GetClassificationTypeFromConsole();

            config.UseOneHotEncode = GetUseOneHotEncodeFromConsole();

            config.ClassifierNumber = GetClassifierNumberFromConsole();

            ClassifierType classifierType = (ClassifierType)Enum.ToObject(typeof(ClassifierType), config.ClassifierNumber - 1);
            config.ClassifierData = new ClassifierData();

            switch (classifierType)
            {
                case ClassifierType.Knn:
                    config.ClassifierData.KnnData = GetKnnDataFromConsole();
                    break;
                case ClassifierType.Mlp:
                    config.ClassifierData.MlpData = GetMlpDataFromConsole();
                    break;
                case ClassifierType.RandomForest:
                    config.ClassifierData.RandomForestData = GetRandomForestData();
                    break;
                case ClassifierType.XGBoost:
                    config.ClassifierData.XGBoostData = GetXGBoostDataFromConsole();
                    break;
            }

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
            config.DeviceNumber = GetDeviceNumberFromConsole(devices);

            Console.WriteLine();
            Console.WriteLine("Enter file name for new config file?");
            string newFilename = Console.ReadLine();
            string ext = newFilename.EndsWith(".json") ? "" : ".json";
            string fullPath = Path.Combine(_configPath, newFilename + ext);

            string json = JsonSerializer.Serialize(config);
            File.WriteAllText(fullPath, json);

            Console.WriteLine();
            Console.WriteLine("Config file created");
        }

        /// <summary>
        /// <para> Check the accuracy of algorithms based on training and test data </para>
        /// </summary>
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
            string testFilename = GetFilename(ListOfCsvFilenames, isTrain: false);
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
            NormalizeFeaturesService normalizeFeaturesService = new NormalizeFeaturesService(0, 1);
            trainTrafficData.Samples = normalizeFeaturesService.NormalizeTrainSamples(trainTrafficData.Samples);
            testTrafficData.Samples = normalizeFeaturesService.NormalizeTestSamples(testTrafficData.Samples);
            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine("\nData normalization time: " + elapsedTime);

            while (true)
            {
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

                while (true)
                {
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

                    bool tryAnotherMetric;
                    while (true)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Try another metric? (yes, no)");
                        string text = Console.ReadLine();
                        if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                        {
                            if (text.ToUpper() == "yes".ToUpper())
                                tryAnotherMetric = true;
                            else
                                tryAnotherMetric = false;
                            break;
                        }
                    }
                    if (tryAnotherMetric == false)
                        break;
                }

                bool tryAnotherClassifier;
                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("Try another classifier? (yes, no)");
                    string text = Console.ReadLine();
                    if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                    {
                        if (text.ToUpper() == "yes".ToUpper())
                            tryAnotherClassifier = true;
                        else
                            tryAnotherClassifier = false;
                        break;
                    }
                }
                if (tryAnotherClassifier == false)
                    break;
            }
        }

        /// <summary>
        /// <para> Getting the name of the training or test file </para>
        /// </summary>
        /// <param name="ListOfFilenames"> list of filenames </param>
        /// <param name="isTrain"> is file has training data </param>
        /// <returns> name of the selected file </returns>
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

        /// <summary>
        /// <para> Getting data from a csv file </para>
        /// </summary>
        /// <param name="filename"></param>
        /// <returns> data from file </returns>
        private static List<string[]> GetDataFromCsvFile(string filename)
        {
            bool hasHeaderRow = GetHasHeaderRowFromConsole();

            return GetDataFromCsvFile(filename, hasHeaderRow);
        }

        /// <summary>
        /// <para> Getting data from a csv file </para>
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="hasHeaderRow"></param>
        /// <returns> data from file </returns>
        private static List<string[]> GetDataFromCsvFile(string filename, bool hasHeaderRow)
        {
            return _csvDataService.GetData(filename, hasHeaderRow);
        }

        /// <summary>
        /// <para> Getting hasHeaderRow </para>
        /// </summary>
        /// <returns> hasHeaderRow </returns>
        private static bool GetHasHeaderRowFromConsole()
        {
            bool hasHeaderRow;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("File has header row? (yes, no)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "yes".ToUpper() || text.ToUpper() == "no".ToUpper())
                {
                    if (text.ToUpper() == "yes".ToUpper())
                        hasHeaderRow = true;
                    else
                        hasHeaderRow = false;
                    break;
                }
            }
            return hasHeaderRow;
        }

        /// <summary>
        /// <para> Getting a traffic data converter based on the data source type </para>
        /// </summary>
        /// <param name="dataSource"> data source type </param>
        /// <returns> traffic data converter </returns>
        private static TrafficDataConverterService GetTrafficDataConverterService(DataSource dataSource)
        {
            ClassificationType classificationType = GetClassificationTypeFromConsole();

            bool useOneHotEncode = GetUseOneHotEncodeFromConsole();

            return GetTrafficDataConverterService(dataSource, classificationType, useOneHotEncode);
        }

        /// <summary>
        /// <para> Getting a traffic data converter based on the data source type </para>
        /// </summary>
        /// <param name="dataSource"> data source type </param>
        /// /// <param name="classificationType"> classification type</param>
        /// /// <param name="useOneHotEncode"> use one hot encode </param>
        /// <returns> traffic data converter </returns>
        private static TrafficDataConverterService GetTrafficDataConverterService(DataSource dataSource, ClassificationType classificationType, bool useOneHotEncode)
        {
            return new TrafficDataConverterService(dataSource, classificationType, useOneHotEncode);
        }

        /// <summary>
        /// <para> Getting a classification type </para>
        /// </summary>
        /// <returns> classification type </returns>
        private static ClassificationType GetClassificationTypeFromConsole()
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

            return classificationType;
        }

        /// <summary>
        /// <para> Getting useOneHotEncode </para>
        /// </summary>
        /// <returns> useOneHotEncode </returns>
        private static bool GetUseOneHotEncodeFromConsole()
        {
            bool useOneHotEncode;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Encode categorical data using OneHotEncode or Skip (OneHotEncode, Skip)");
                string text = Console.ReadLine();
                if (text.ToUpper() == "OneHotEncode".ToUpper() || text.ToUpper() == "Skip".ToUpper())
                {
                    if (text.ToUpper() == "OneHotEncode".ToUpper())
                        useOneHotEncode = true;
                    else
                        useOneHotEncode = false;
                    break;
                }
            }
            return useOneHotEncode;
        }

        /// <summary>
        /// <para> Get data source type based on filename </para>
        /// </summary>
        /// <param name="filename"></param>
        /// <returns> data source type </returns>
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

        /// <summary>
        /// <para> Select a classifier </para>
        /// </summary>
        /// <returns> classifier class </returns>
        private static IClassifierService GetClassifier()
        {
            int classifierNumber = GetClassifierNumberFromConsole();

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

        /// <summary>
        /// <para> Select a classifier </para>
        /// </summary>
        /// /// <param name="classifierNumber"> classifier number </param>
        /// /// <param name="classifierData"> classifier data </param>
        /// <returns> classifier class </returns>
        private static IClassifierService GetClassifier(int classifierNumber, ClassifierData classifierData)
        {
            ClassifierType classifierType = (ClassifierType)Enum.ToObject(typeof(ClassifierType), classifierNumber - 1);

            switch (classifierType)
            {
                case ClassifierType.Knn:
                    return new KnnService(classifierData.KnnData.NumberOfNeighbors);
                case ClassifierType.Mlp:
                    return new MlpService(classifierData.MlpData.HiddenLayersWithNeurons, classifierData.MlpData.Alpha, classifierData.MlpData.BatchSize, classifierData.MlpData.LearningRate,
                                          classifierData.MlpData.MaxIterations, classifierData.MlpData.Tol, classifierData.MlpData.Beta_1, classifierData.MlpData.Beta_2, classifierData.MlpData.Epsilon); ;
                case ClassifierType.RandomForest:
                    return new RandomForestService(classifierData.RandomForestData.NumberOfTrees, classifierData.RandomForestData.MaxDepth, 
                                                   classifierData.RandomForestData.MinSize, classifierData.RandomForestData.PartOfTrafficDataRatio);
                case ClassifierType.XGBoost:
                    return new XGBoostService(classifierData.XGBoostData.Rounds, classifierData.XGBoostData.MaxDepth, classifierData.XGBoostData.MinSize, classifierData.XGBoostData.LearningRate,
                                              classifierData.XGBoostData.Lambda, classifierData.XGBoostData.Gamma, classifierData.XGBoostData.NFeatureRatio); ;
            }

            return null;
        }

        /// <summary>
        /// <para> Getting classifier number </para>
        /// </summary>
        /// <returns> classifier number</returns>
        private static int GetClassifierNumberFromConsole()
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

            return classifierNumber;
        }

        /// <summary>
        /// <para> Getting a KNN classifier with parameters (by default or changed)  </para>
        /// </summary>
        /// <returns> classifier class </returns>
        private static IClassifierService KnnClassifier()
        {
            KnnData knnData = GetKnnDataFromConsole();

            Console.WriteLine("Initialization Knn...");
            return new KnnService(knnData.NumberOfNeighbors);
        }

        /// <summary>
        /// <para> Getting a KNN Data  </para>
        /// </summary>
        /// <returns> KNN Data </returns>
        private static KnnData GetKnnDataFromConsole()
        {
            KnnData knnData = new KnnData();

            knnData.NumberOfNeighbors = 5;
            knnData.NumberOfNeighbors = TryReadValueFromConsole("Enter number of neighbors", knnData.NumberOfNeighbors);

            return knnData;
        }

        /// <summary>
        /// <para> Getting a MLP classifier with parameters (by default or changed)  </para>
        /// </summary>
        /// <returns> classifier class </returns>
        private static IClassifierService MlpClassifier()
        {
            MlpData mlpData = GetMlpDataFromConsole();

            Console.WriteLine("Initialization multilayer perceptron...");
            return new MlpService(mlpData.HiddenLayersWithNeurons, mlpData.Alpha, mlpData.BatchSize, mlpData.LearningRate, 
                                  mlpData.MaxIterations, mlpData.Tol, mlpData.Beta_1, mlpData.Beta_2, mlpData.Epsilon);
        }

        /// <summary>
        /// <para> Getting a MLP Data </para>
        /// </summary>
        /// <returns> MLP Data </returns>
        private static MlpData GetMlpDataFromConsole()
        {
            MlpData mlpData = new MlpData();
            int numberOfLayers = 1;
            bool defaulthiddenLayers = false;
            while (true)
            {
                Console.WriteLine("Enter number of hidden layers (Default: {0} hidden layers - (100)):", numberOfLayers);
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
            if (defaulthiddenLayers == false)
            {
                mlpData.HiddenLayersWithNeurons = new List<int>(numberOfLayers);
                int i = 1;
                while (mlpData.HiddenLayersWithNeurons.Count != numberOfLayers)
                {
                    Console.WriteLine("Enter number of Neurons in the {0} hidden layer:", i);
                    if (Int32.TryParse(Console.ReadLine(), out int numberOfNeurons))
                        if (numberOfNeurons > 0)
                        {
                            mlpData.HiddenLayersWithNeurons.Add(numberOfNeurons);
                            i++;
                        }
                }
            }
            else
            {
                mlpData.HiddenLayersWithNeurons = new List<int> { 100 };
            }

            mlpData.Alpha = 0.0001;
            mlpData.Alpha = TryReadValueFromConsole("Enter alpha", mlpData.Alpha);

            mlpData.BatchSize = 200;
            mlpData.BatchSize = TryReadValueFromConsole("Enter batch size", mlpData.BatchSize);

            mlpData.LearningRate = 0.001;
            mlpData.LearningRate = TryReadValueFromConsole("Enter learning rate", mlpData.LearningRate);

            mlpData.MaxIterations = 200;
            mlpData.MaxIterations = TryReadValueFromConsole("Enter max iterations", mlpData.MaxIterations);

            mlpData.Tol = 0.0001;
            mlpData.Tol = TryReadValueFromConsole("Enter tol", mlpData.Tol);

            mlpData.Beta_1 = 0.9;
            mlpData.Beta_1 = TryReadValueFromConsole("Enter beta_1", mlpData.Beta_1);

            mlpData.Beta_2 = 0.999;
            mlpData.Beta_2 = TryReadValueFromConsole("Enter beta_2", mlpData.Beta_2);

            mlpData.Epsilon = 0.00000001;
            mlpData.Epsilon = TryReadValueFromConsole("Enter epsilon", mlpData.Epsilon);

            return mlpData;
        }

        /// <summary>
        /// <para> Getting a Random Forest classifier with parameters (by default or changed)  </para>
        /// </summary>
        /// <returns> classifier class </returns>
        private static IClassifierService RandomForestClassifier()
        {
            RandomForestData randomForestData = GetRandomForestData();

            Console.WriteLine("Initialization random forest...");
            return new RandomForestService(randomForestData.NumberOfTrees, randomForestData.MaxDepth, randomForestData.MinSize, randomForestData.PartOfTrafficDataRatio);
        }

        /// <summary>
        /// <para> Getting a Random Forest Data </para>
        /// </summary>
        /// <returns> Random Forest Data </returns>
        private static RandomForestData GetRandomForestData()
        {
            RandomForestData randomForestData = new RandomForestData();

            randomForestData.NumberOfTrees = 5;
            randomForestData.NumberOfTrees = TryReadValueFromConsole("Enter num Trees", randomForestData.NumberOfTrees);

            randomForestData.MaxDepth = 3;
            randomForestData.MaxDepth = TryReadValueFromConsole("Enter max depth", randomForestData.MaxDepth);

            randomForestData.MinSize = 1;
            randomForestData.MinSize = TryReadValueFromConsole("Enter min size", randomForestData.MinSize);

            randomForestData.PartOfTrafficDataRatio = 0.2;
            randomForestData.PartOfTrafficDataRatio = TryReadValueFromConsole("Enter part of traffic data ratio", randomForestData.PartOfTrafficDataRatio);

            return randomForestData;
        }

        /// <summary>
        /// <para> Getting a XGBoost classifier with parameters (by default or changed)  </para>
        /// </summary>
        /// <returns> classifier class </returns>
        private static IClassifierService XGBoostClassifier()
        {
            XGBoostData xgBoostData = GetXGBoostDataFromConsole();

            Console.WriteLine("Initialization XGBoost...");
            return new XGBoostService(xgBoostData.Rounds, xgBoostData.MaxDepth, xgBoostData.MinSize, xgBoostData.LearningRate,
                                      xgBoostData.Lambda, xgBoostData.Gamma, xgBoostData.NFeatureRatio);
        }

        /// <summary>
        /// <para> Getting a XGBoost Data </para>
        /// </summary>
        /// <returns> XGBoost Data </returns>
        private static XGBoostData GetXGBoostDataFromConsole()
        {
            XGBoostData xgBoostData = new XGBoostData();

            xgBoostData.Rounds = 5;
            xgBoostData.Rounds = TryReadValueFromConsole("Enter rounds", xgBoostData.Rounds);

            xgBoostData.MaxDepth = 5;
            xgBoostData.MaxDepth = TryReadValueFromConsole("Enter max depth", xgBoostData.MaxDepth);

            xgBoostData.MinSize = 1;
            xgBoostData.MinSize = TryReadValueFromConsole("Enter min size", xgBoostData.MinSize);

            xgBoostData.LearningRate = 0.4;
            xgBoostData.LearningRate = TryReadValueFromConsole("Enter learning rate", xgBoostData.LearningRate);

            xgBoostData.Lambda = 1.5;
            xgBoostData.Lambda = TryReadValueFromConsole("Enter lambda", xgBoostData.Lambda);

            xgBoostData.Gamma = 1;
            xgBoostData.Gamma = TryReadValueFromConsole("Enter gamma", xgBoostData.Gamma);

            xgBoostData.NFeatureRatio = 0.2;
            xgBoostData.NFeatureRatio = TryReadValueFromConsole("Enter n feature ratio", xgBoostData.NFeatureRatio);

            return xgBoostData;
        }

        /// <summary>
        /// <para> Calculate accuracy based on labels </para>
        /// </summary>
        /// <param name="metric"> type of metric </param>
        /// <param name="trueLabels"> correct labels </param>
        /// <param name="predictedLabels"> predicted labels </param>
        /// <returns> accuracy </returns>
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

        /// <summary>
        /// <para> Create a file based on incoming real traffic </para>
        /// </summary>
        private static void CreateCaptureFileFromRealTimeTraffic()
        {
            Console.Write("-- Please enter the output file name: ");
            string filename = Console.ReadLine();
            LibPcapLiveDevice selectedDevice = SelectDevice();

            string ext = (filename.EndsWith(".pcap") || filename.EndsWith(".pcapng")) ? "" : ".pcap";
            filename = DataSource.RealTime.ToString().ToUpper() + "_" + filename + ext;

            CreateCaptureFileFromRealtimeTraffic(filename, selectedDevice, 0);
        }

        /// <summary>
        /// <para> Select available devices on this machine </para>
        /// </summary>
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

            int deviceNumber = GetDeviceNumberFromConsole(devices);

            return devices[deviceNumber];
        }

        /// <summary>
        /// <para> Getting a device number </para>
        /// </summary>
        /// <returns> device number </returns>
        private static int GetDeviceNumberFromConsole(LibPcapLiveDeviceList devices)
        {
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
            return deviceNumber;
        }

        /// <summary>
        /// <para> Create pcap file based on incoming real traffic </para>
        /// </summary>
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
        /// <para> Write the packet to the file </para>
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

                Console.WriteLine("{0} At: UTC/GMT {1}:{2}: MAC:{3} -> MAC:{4}",
                                  _packetIndex_option,
                                  rawPacket.Timeval.Date.ToString(),
                                  rawPacket.Timeval.Date.Millisecond,
                                  ethernetPacket.SourceHardwareAddress,
                                  ethernetPacket.DestinationHardwareAddress);
                _packetIndex_option++;
            }
        }

        /// <summary>
        /// <para> Creating a csv file based on pcap files </para>
        /// </summary>
        private static void CreateCsvFromPcapFiles()
        {
            string[] pcapFilenames = _pcapDataService.GetFilenameList();
            List<string> selectedPcapFilenames = new List<string>();
            List<string[]> newData = new List<string[]>();

            pcapFilenames = pcapFilenames.Where(f => f.ToUpper().StartsWith(DataSource.RealTime.ToString().ToUpper()))
                                         .Where(f => f.ToUpper().Contains(_reservedPcapFilename) == false)
                                         .ToArray();
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

        /// <summary>
        /// <para> Getting int value </para>
        /// </summary>
        /// <param name="question"> question </param>
        /// <param name="value"> default value </param>
        /// <returns> int value </returns>
        private static int TryReadValueFromConsole(string question, int value)
        {
            while (true)
            {
                Console.WriteLine(question + " (Default: {0}):", value);
                string text = Console.ReadLine();
                if (text == "")
                    break;
                if (Int32.TryParse(text, out int parseValue))
                    if (parseValue > 0)
                    {
                        value = parseValue;
                        break;
                    }
            }

            return value;
        }

        /// <summary>
        /// <para> Getting double value </para>
        /// </summary>
        /// <param name="question"> question </param>
        /// <param name="value"> default value </param>
        /// <returns> double value </returns>
        private static double TryReadValueFromConsole(string question, double value)
        {
            while (true)
            {
                Console.WriteLine(question + " (Default: {0}):", value);
                string text = Console.ReadLine();
                if (text == "")
                    break;
                if (Double.TryParse(text, out double parseValue))
                    if (parseValue > 0)
                    {
                        value = parseValue;
                        break;
                    }
            }

            return value;
        }

        /// <summary>
        /// <para> Getting int value between min and max values </para>
        /// </summary>
        /// <param name="question"> question </param>
        /// <param name="min"> min </param>
        /// /// <param name="max"> max </param>
        /// <returns> int value </returns>
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

        /// <summary>
        /// Create program header
        /// </summary>
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
