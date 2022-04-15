using IDS.BusinessLogic.Services;
using IDS.DataAccess.CSV;
using IDS.DataAccess.PCAP;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Tests
{
    public class DataServiceTests
    {
        [Test]
        public void GetData_Csv_ShouldReturnTrue()
        {
            // arrange
            string csvFile = "UNSW_NB15_training-set.csv";

            DataAccess.CSV.DataRepository csvDataRepository = new DataAccess.CSV.DataRepository(
                new CsvSettings("..\\..\\..\\..\\IDS.DataAccess.CSV\\CsvData", ',')
            );
            DataService dataService = new DataService(csvDataRepository);

            //act
            List<string[]> data = dataService.GetData(csvFile, hasHeaderRow: true);

            // assert
            Assert.IsTrue(data != null);
        }

        [Test]
        public void GetData_Pcap_ShouldReturnTrue()
        {
            // arrange
            string pcapFile = "REALTIME_myTraffic.pcapng";

            DataAccess.PCAP.DataRepository csvDataRepository = new DataAccess.PCAP.DataRepository(
                "..\\..\\..\\..\\IDS.DataAccess.PCAP\\PcapData"
            );
            DataService dataService = new DataService(csvDataRepository);

            //act
            List<string[]> data = dataService.GetData(pcapFile);

            // assert
            Assert.IsTrue(data != null);
        }
    }
}
