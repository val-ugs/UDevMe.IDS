﻿using IDS.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class DataService : IDataService
    {
        private readonly IDataRepository _dataRepository;

        public DataService(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;   
        }

        public bool Create(string fileName, double[] dataRow)
        {
            bool isValid = dataRow != null;

            if (isValid)
                _dataRepository.Create(fileName, dataRow);

            return isValid;
        }

        public List<string[]> GetData(string fileName, bool hasHeaderRow = false)
        {
            return _dataRepository.GetData(fileName, hasHeaderRow);
        }
    }
}
