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

        public double GetHouseholdInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            return GetIndoorInfectionProbability(infectingCitizen, infectionCandidate);
        }

        public double GetIndoorInfectionProbabilityNoContact(uint infectingCitizen, uint infectionCandidate)
        {
            int maskBehaviorInfecting = GetMaskBehavior(infectingCitizen);
            int maskBehaviorCandidate = GetMaskBehavior(infectionCandidate);
            int modifier = GetMaskModifier(maskBehaviorInfecting, maskBehaviorCandidate);
            double modificationIndoor = GetIndoorMaskModification(modifier);
            return 1 - Math.Pow(1 - (Config.IndoorDiseaseTransmissionProbability / 100.0 / 96.0 / modificationIndoor), stepLengthInHours);
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
                    int modifier = GetMaskModifier(i, j);

                    double modificationOutdoor = GetOutdoorMaskModification(modifier);
                    double modificationIndoor = GetIndoorMaskModification(modifier);
                    double modificationVehicle = GetVehicleMaskModification(modifier);

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
            _allConsideredCitizens.Clear();
            _masksOthersProtection.Clear();
            _masksOwnProtection.Clear();
            stepLengthInHours = -1;
            indoorInfectionProbabilityNoContact = 0;

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

        /// <summary>Starts a new deterministic mask-assignment random stream.</summary>
        internal void ResetRandom(int seed)
        {
            random = new System.Random(seed);
        }

        private int GetMaskModifier(int infectingBehavior, int candidateBehavior)
        {
            int modifier = 0;
            if (infectingBehavior != 0)
            {
                modifier++;
            }

            if (candidateBehavior == 2)
            {
                modifier++;
            }

            return modifier;
        }

        private double GetOutdoorMaskModification(int modifier)
        {
            if (Config.MaskBehavior == RealTime.Config.MaskBehavior.Full)
            {
                return Math.Pow(Config.TransmissionProbabilityReduction, modifier);
            }

            return 1;
        }

        private double GetIndoorMaskModification(int modifier)
        {
            if (Config.MaskBehavior == RealTime.Config.MaskBehavior.Full
                || Config.MaskBehavior == RealTime.Config.MaskBehavior.Vehicle
                || Config.MaskBehavior == RealTime.Config.MaskBehavior.Building)
            {
                return Math.Pow(Config.TransmissionProbabilityReduction, modifier);
            }

            return 1;
        }

        private double GetVehicleMaskModification(int modifier)
        {
            if (Config.MaskBehavior == RealTime.Config.MaskBehavior.Full
                || Config.MaskBehavior == RealTime.Config.MaskBehavior.Vehicle)
            {
                return Math.Pow(Config.TransmissionProbabilityReduction, modifier);
            }

            return 1;
        }
    }
}
