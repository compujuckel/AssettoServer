using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RandomDynamicTrafficPlugin
{    
    public class RandomDynamicTrafficHelperFunctions
    {
        private readonly RandomDynamicTrafficConfiguration _configuration;
        private readonly TrafficMap _trafficMap;
        private readonly ACServerConfiguration _acServerConfiguration;
        private Random _random = new Random();
        private readonly int _totalWeights = 0;
        private readonly EntryCarManager _entryCarManager;

        public List<DensityAdjustmentConfig> _densityAdjustmentConfigs = new List<DensityAdjustmentConfig>();

        public RandomDynamicTrafficHelperFunctions(RandomDynamicTrafficConfiguration configuration, ACServerConfiguration acServerConfiguration, TrafficMap trafficMap, EntryCarManager entryCarManager)
        {
            _configuration = configuration;
            _acServerConfiguration = acServerConfiguration;
            _trafficMap = trafficMap;
            _entryCarManager = entryCarManager;

            _densityAdjustmentConfigs.Add(new DensityAdjustmentConfig
            {
                Type = DensityAdjustmentType.Low,
                Weight = 5
            });
            _densityAdjustmentConfigs.Add(new DensityAdjustmentConfig
            {
                Type = DensityAdjustmentType.Casual,
                Weight = 10
            });
            _densityAdjustmentConfigs.Add(new DensityAdjustmentConfig
            {
                Type = DensityAdjustmentType.Peak,
                Weight = 3
            });
            _densityAdjustmentConfigs.Add(new DensityAdjustmentConfig
            {
                Type = DensityAdjustmentType.Accident,
                Weight = 2
            });
            _densityAdjustmentConfigs.Add(new DensityAdjustmentConfig
            {
                Type = DensityAdjustmentType.AdjustWithAverageSpeed,
                Weight = 80
            });
            _totalWeights = _densityAdjustmentConfigs.Sum(x => x.Weight);
        }

        public float CalculateAverageSpeeds()
        {
            var averageSpeed = _configuration.MiddlePointSpeed;
            var totalSpeeds = 0f;
            var totalCars = 0;
            foreach(var car in _entryCarManager.EntryCars)
            {
                if (car.AiControlled || car.Client == null || !car.Client.HasSentFirstUpdate) continue;
                
                var carSpeed = (int)(car.Status.Velocity.Length() * 3.6);
                Log.Debug("Speed of {Name} is {carSpeed}",car.Client.Name, carSpeed);
                //if (IsCarCloseToSpline(car.EntryCar) && carSpeed > 40)
                if (carSpeed >= 40)
                {
                    totalSpeeds += carSpeed;
                    totalCars++;
                }                
            }

            if (totalCars > 0)
                averageSpeed = totalSpeeds / totalCars;

            Log.Debug("Average speed calculation of {averageSpeed}, using {totalCars} cars", averageSpeed, totalCars);
            return averageSpeed;            
        }

        public float CalculateNewDensity(List<EntryCar> cars, float currentDensity)
        {            
            var randomNum = _random.Next(0, _totalWeights + 1);
            DensityAdjustmentConfig? selectedConfig = null;

            foreach(var config in _densityAdjustmentConfigs.OrderByDescending(x => x.Weight))
            {
                if (randomNum <= config.Weight)
                {
                    selectedConfig = config;
                    break;
                } else
                {
                    randomNum -= config.Weight;
                }
            }

            float newDensity = 0;
            switch (selectedConfig?.Type)
            {
                case DensityAdjustmentType.Low:
                    newDensity = GetRandomNumber(0, _configuration.LowTrafficDensity);
                    break;
                case DensityAdjustmentType.Casual:
                    newDensity = GetRandomNumber(_configuration.LowTrafficDensity + 0.1f, _configuration.CasualTrafficDensity);
                    break;
                case DensityAdjustmentType.Peak:
                    newDensity = GetRandomNumber(_configuration.CasualTrafficDensity + 0.1f, _configuration.PeakTrafficDensity);
                    break;
                case DensityAdjustmentType.Accident:
                    newDensity = GetRandomNumber(_configuration.PeakTrafficDensity + 0.1f, _configuration.MaxTrafficDensity);
                    break;
                case DensityAdjustmentType.AdjustWithAverageSpeed:
                    var averageSpeed = CalculateAverageSpeeds(); // 300                     
                    newDensity = AdjustDensity(currentDensity, averageSpeed);
                    break;
                default:
                    newDensity = currentDensity;
                    break;
            }

            Log.Debug("Setting density from {currentDensity} to {newDensity} using config {selectedConfig}", currentDensity, newDensity, selectedConfig?.Type.ToString());
            return Math.Clamp(newDensity, _configuration.MinTrafficDensity, _configuration.MaxTrafficDensity);
        }

        public float AdjustDensity(float currentDensity, float averageSpeed)
        {            
            if (averageSpeed >= _configuration.MiddlePointSpeed)
            {
                //Too fast, adding more traffic
                var ratio = (averageSpeed - _configuration.MiddlePointSpeed) / _configuration.MiddlePointSpeed;

                var adjustmentAmount = ratio * _configuration.MaxSpeedDensityAdjustment;
                currentDensity += adjustmentAmount;
            }
            else
            {                
                //too slow, reducing traffic
                var ratio = 1 - (averageSpeed / _configuration.MiddlePointSpeed);

                var adjustmentAmount = ratio * _configuration.MaxSpeedDensityAdjustment;
                currentDensity -= adjustmentAmount;
            }
            return currentDensity;
        }

        public bool IsCarCloseToSpline(EntryCar car)
        {
            var targetPlayerSplinePos = _trafficMap.WorldToSpline(car.Status.Position);
           
            if (targetPlayerSplinePos.DistanceSquared > _acServerConfiguration.Extra.AiParams.MaxPlayerDistanceToAiSplineSquared)
            {
                return false;
            }
            return true;
        }

        public float GetRandomNumber(float minimum, float maximum)
        {
            Random random = new Random();
            return (float)random.NextDouble() * (maximum - minimum) + minimum;
        }
    }

    public class DensityAdjustmentConfig
    {
        public int Weight;
        public DensityAdjustmentType Type;
    }

    public enum DensityAdjustmentType
    {
        Low,
        Casual,
        Peak,
        Accident,
        AdjustWithAverageSpeed        
    }
}
