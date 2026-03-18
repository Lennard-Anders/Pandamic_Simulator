using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RealTime.Config;

namespace RealTime.Pandemic
{
    class MaskManager
    {
        private HashSet<uint> _allConsideredCitizens = new HashSet<uint>();
        private HashSet<uint> _masksOthersProtection = new HashSet<uint>();
        private HashSet<uint> _masksOwnProtection = new HashSet<uint>();

        private System.Random random = new System.Random(1337);

        private double stepLengthInHours = -1;

        private double[][] outdoorTransmissionProbability = new double[3][];
        private double[][] indoorTransmissionProbability = new double[3][];
        private double[][] vehicleTransmissionProbability = new double[3][];

        private double indoorInfectionProbabilityNoContact;

        private Config.RealTimeConfig Config;

        private int GetMaskBehavior(uint citizen)
        {
            if (!_allConsideredCitizens.Contains(citizen))
            {
                double sum = Config.RatioIgnoreMasks + Config.RatioOtherProtectionMask + Config.RatioOwnProtectionMask;
                double randomValue = random.NextDouble();
                if (randomValue < Config.RatioOtherProtectionMask / sum)
                {
                    _masksOthersProtection.Add(citizen);
                } else if (randomValue < (Config.RatioOtherProtectionMask + Config.RatioOwnProtectionMask) / sum)
                {
                    _masksOwnProtection.Add(citizen);
                }
                _allConsideredCitizens.Add(citizen);
            }

            if (_masksOthersProtection.Contains(citizen))
            {
                return 1;
            } else if (_masksOwnProtection.Contains(citizen))
            {
                return 2;
            } else
            {
                return 0;
            }
        }

        public double GetOutdoorInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            int maskBehaviorInfecting = GetMaskBehavior(infectingCitizen);
            int maskBehaviorCandidate = GetMaskBehavior(infectionCandidate);

            return outdoorTransmissionProbability[maskBehaviorInfecting][maskBehaviorCandidate];
        }

        public double GetIndoorInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            int maskBehaviorInfecting = GetMaskBehavior(infectingCitizen);
            int maskBehaviorCandidate = GetMaskBehavior(infectionCandidate);

            return indoorTransmissionProbability[maskBehaviorInfecting][maskBehaviorCandidate];
        }

        public double GetVehicleInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            int maskBehaviorInfecting = GetMaskBehavior(infectingCitizen);
            int maskBehaviorCandidate = GetMaskBehavior(infectionCandidate);

            return vehicleTransmissionProbability[maskBehaviorInfecting][maskBehaviorCandidate];
        }

        public double GetDefaultIndoorInfectionProbability()
        {
            return indoorTransmissionProbability[0][0];
        }

        public double GetIndoorInfectionProbabilityNoContact()
        {
            return indoorInfectionProbabilityNoContact;
        }

        public bool IsWearingMask(uint citizenId)
        {
            return _masksOthersProtection.Contains(citizenId) || _masksOwnProtection.Contains(citizenId);
        }

        public void SetMaskForCitizen(uint citizenId, bool masked)
        {
            // Mark as considered so the random assignment in GetMaskBehavior doesn't override this
            _allConsideredCitizens.Add(citizenId);
            if (masked)
            {
                _masksOthersProtection.Add(citizenId);
                _masksOwnProtection.Remove(citizenId);
            }
            else
            {
                _masksOthersProtection.Remove(citizenId);
                _masksOwnProtection.Remove(citizenId);
            }
        }

        public void SetStepLengthInHours(double stepLengthInHours)
        {
            this.stepLengthInHours = stepLengthInHours;

            for (int i = 0; i < outdoorTransmissionProbability.Length; i++)
            {
                for (int j = 0; j < outdoorTransmissionProbability[i].Length; j++)
                {
                    int modifier = 0;
                    if (i != 0)
                    {
                        modifier += 1;
                    }
                    if (j == 2)
                    {
                        modifier += 1;
                    }

                    double modificationOutdoor = 1;
                    double modificationIndoor = 1;
                    double modificationVehicle = 1;
                    if (Config.MaskBehavior == RealTime.Config.MaskBehavior.Full)
                    {
                        modificationOutdoor = Math.Pow(Config.TransmissionProbabilityReduction, modifier);
                        modificationIndoor = Math.Pow(Config.TransmissionProbabilityReduction, modifier);
                        modificationVehicle = Math.Pow(Config.TransmissionProbabilityReduction, modifier);
                    }
                    else if (Config.MaskBehavior == RealTime.Config.MaskBehavior.Vehicle)
                    {
                        modificationIndoor = Math.Pow(Config.TransmissionProbabilityReduction, modifier);
                        modificationVehicle = Math.Pow(Config.TransmissionProbabilityReduction, modifier);
                    }
                    else if (Config.MaskBehavior == RealTime.Config.MaskBehavior.Building)
                    {
                        modificationIndoor = Math.Pow(Config.TransmissionProbabilityReduction, modifier);
                    }

                    outdoorTransmissionProbability[i][j] = 1 - Math.Pow(1 - (Config.OutdoorDiseaseTransmissionProbability / 100.0 / modificationOutdoor), stepLengthInHours);
                    indoorTransmissionProbability[i][j] = 1 - Math.Pow(1 - (Config.IndoorDiseaseTransmissionProbability / 100.0 / modificationIndoor), stepLengthInHours);
                    indoorInfectionProbabilityNoContact = 1 - Math.Pow(1 - (Config.IndoorDiseaseTransmissionProbability / 100.0 / 96.0), stepLengthInHours);
                    vehicleTransmissionProbability[i][j] = 1 - Math.Pow(1 - (Config.IndoorDiseaseTransmissionProbability / 100.0 / modificationVehicle), stepLengthInHours);
                }
            }
        }

        internal void Init(RealTimeConfig config)
        {
            this.Config = config;

            for (int i = 0; i < outdoorTransmissionProbability.Length; i++)
            {
                outdoorTransmissionProbability[i] = new double[3];
            }

            for (int i = 0; i < indoorTransmissionProbability.Length; i++)
            {
                indoorTransmissionProbability[i] = new double[3];
            }

            for (int i = 0; i < vehicleTransmissionProbability.Length; i++)
            {
                vehicleTransmissionProbability[i] = new double[3];
            }
        }
    }
}
