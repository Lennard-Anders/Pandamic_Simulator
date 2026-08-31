namespace RealTime.Pandemic
{
    using System;
    using RealTime.Config;

    /// <summary>Compatibility facade around the Unity-independent <see cref="MaskEngine"/>.</summary>
    internal sealed class MaskManager
    {
        private readonly MaskEngine engine = new MaskEngine();
        private int masterSeed = 1337;

        public double GetOutdoorInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            return engine.GetTransmissionProbability(infectingCitizen, infectionCandidate, MaskTransmissionEnvironment.Outdoor);
        }

        public double GetIndoorInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            return engine.GetTransmissionProbability(infectingCitizen, infectionCandidate, MaskTransmissionEnvironment.Indoor);
        }

        public double GetVehicleInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            return engine.GetTransmissionProbability(infectingCitizen, infectionCandidate, MaskTransmissionEnvironment.Vehicle);
        }

        public double GetDefaultIndoorInfectionProbability()
        {
            return engine.GetUnprotectedTransmissionProbability(MaskTransmissionEnvironment.Indoor);
        }

        public double GetHouseholdInfectionProbability(uint infectingCitizen, uint infectionCandidate)
        {
            return GetIndoorInfectionProbability(infectingCitizen, infectionCandidate);
        }

        public double GetIndoorInfectionProbabilityNoContact(uint infectingCitizen, uint infectionCandidate)
        {
            return engine.GetTransmissionProbability(infectingCitizen, infectionCandidate, MaskTransmissionEnvironment.ResidentialSharedArea);
        }

        public bool IsWearingMask(uint citizenId)
        {
            return engine.GetAssignment(citizenId) != MaskProtectionType.None;
        }

        public void SetMaskForCitizen(uint citizenId, bool masked)
        {
            engine.SetOverride(citizenId, masked);
        }

        public void SetStepLengthInHours(double stepLengthInHours)
        {
            engine.SetStepLengthHours(stepLengthInHours);
        }

        internal void Init(RealTimeConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            engine.Reset(new MaskPolicy
            {
                Behavior = config.MaskBehavior,
                TransmissionReductionFactor = config.TransmissionProbabilityReduction,
                IgnorePercent = config.RatioIgnoreMasks,
                SourceControlPercent = config.RatioOtherProtectionMask,
                PersonalProtectionPercent = config.RatioOwnProtectionMask,
                IndoorProbabilityPerHour = config.IndoorDiseaseTransmissionProbability / 100d,
                OutdoorProbabilityPerHour = config.OutdoorDiseaseTransmissionProbability / 100d,
                ResidentialSharedAreaMultiplier = config.ResidentialSharedAreaTransmissionMultiplier,
            }, masterSeed);
        }

        internal void ResetStableTraits(int seed)
        {
            if (seed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed));
            }

            masterSeed = seed;
            engine.ResetStableTraits(seed);
        }

        internal int GetMaskBehaviorForTesting(uint citizenId)
        {
            return (int)engine.GetAssignment(citizenId);
        }

        internal string GetMaskTypeName(uint citizenId)
        {
            switch (engine.GetAssignment(citizenId))
            {
                case MaskProtectionType.SourceControl:
                    return "OtherProtection";
                case MaskProtectionType.PersonalProtection:
                    return "OwnProtection";
                default:
                    return "None";
            }
        }
    }
}
