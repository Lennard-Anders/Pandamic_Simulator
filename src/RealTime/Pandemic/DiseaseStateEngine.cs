// <copyright file="DiseaseStateEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;

    /// <summary>The mutually exclusive epidemiological state of one tracked citizen.</summary>
    internal enum DiseaseState
    {
        Susceptible,
        Exposed,
        Infectious,
        PostInfectiousIll,
        Recovered,
        Dead,
        Removed,
    }

    /// <summary>The symptom state, which is independent of the primary disease state.</summary>
    internal enum SymptomState
    {
        None,
        PreSymptomatic,
        Symptomatic,
        PostSymptomatic,
    }

    /// <summary>The healthcare state recorded on a disease course.</summary>
    internal enum HospitalizationState
    {
        None,
        SeekingCare,
        Hospitalized,
        CareUnavailable,
        Discharged,
    }

    /// <summary>Identifies whether an exposure was seeded or caused by simulated transmission.</summary>
    internal enum DiseaseExposureKind
    {
        InitialSeed,
        SecondaryTransmission,
    }

    /// <summary>An immutable disease timeline with mutable terminal/progression state.</summary>
    internal sealed class DiseaseCourse
    {
        public DiseaseCourse(
            uint citizenId,
            DateTime exposureTime,
            DateTime infectiousStartTime,
            DateTime infectiousEndTime,
            DateTime? symptomStartTime,
            DateTime? symptomEndTime,
            DateTime recoveryTime,
            bool isSymptomatic,
            DiseaseExposureKind exposureKind)
        {
            if (citizenId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(citizenId));
            }

            if (infectiousStartTime < exposureTime)
            {
                throw new ArgumentException("InfectiousStartTime must not precede ExposureTime.", nameof(infectiousStartTime));
            }

            if (infectiousEndTime <= infectiousStartTime)
            {
                throw new ArgumentException("InfectiousEndTime must be later than InfectiousStartTime.", nameof(infectiousEndTime));
            }

            if (recoveryTime < infectiousEndTime)
            {
                throw new ArgumentException("RecoveryTime must not precede InfectiousEndTime.", nameof(recoveryTime));
            }

            if (isSymptomatic)
            {
                if (!symptomStartTime.HasValue || !symptomEndTime.HasValue)
                {
                    throw new ArgumentException("Symptomatic courses require symptom start and end times.", nameof(symptomStartTime));
                }

                if (symptomStartTime.Value < exposureTime || symptomEndTime.Value <= symptomStartTime.Value)
                {
                    throw new ArgumentException("The symptom interval is invalid.", nameof(symptomStartTime));
                }

                if (symptomEndTime.Value > recoveryTime)
                {
                    throw new ArgumentException("SymptomEndTime must not follow RecoveryTime.", nameof(symptomEndTime));
                }
            }
            else if (symptomStartTime.HasValue || symptomEndTime.HasValue)
            {
                throw new ArgumentException("Asymptomatic courses cannot contain a symptom interval.", nameof(symptomStartTime));
            }

            CitizenId = citizenId;
            ExposureTime = exposureTime;
            InfectiousStartTime = infectiousStartTime;
            InfectiousEndTime = infectiousEndTime;
            SymptomStartTime = symptomStartTime;
            SymptomEndTime = symptomEndTime;
            RecoveryTime = recoveryTime;
            IsSymptomatic = isSymptomatic;
            ExposureKind = exposureKind;
            HospitalizationState = HospitalizationState.None;
            MortalityStartTime = isSymptomatic ? symptomStartTime.Value : infectiousStartTime;
            CurrentDiseaseState = DiseaseState.Exposed;
        }

        public uint CitizenId { get; }

        public DateTime ExposureTime { get; }

        public DateTime InfectiousStartTime { get; }

        public DateTime InfectiousEndTime { get; }

        public DateTime? SymptomStartTime { get; }

        public DateTime? SymptomEndTime { get; }

        public DateTime RecoveryTime { get; }

        public bool IsSymptomatic { get; }

        public DiseaseExposureKind ExposureKind { get; }

        public HospitalizationState HospitalizationState { get; set; }

        public DateTime MortalityStartTime { get; internal set; }

        public DateTime? DeathTime { get; private set; }

        public DiseaseState CurrentDiseaseState { get; internal set; }

        public void ScheduleDeath(DateTime deathTime)
        {
            if (deathTime < InfectiousStartTime || deathTime >= RecoveryTime)
            {
                throw new ArgumentOutOfRangeException(nameof(deathTime), "Death must occur during the unresolved disease course.");
            }

            DeathTime = deathTime;
        }

        public bool IsWithinInfectiousInterval(DateTime simulationTime)
        {
            return simulationTime >= InfectiousStartTime && simulationTime < InfectiousEndTime;
        }

        public SymptomState GetSymptomState(DateTime simulationTime)
        {
            if (!IsSymptomatic)
            {
                return SymptomState.None;
            }

            if (simulationTime < SymptomStartTime.Value)
            {
                return SymptomState.PreSymptomatic;
            }

            if (simulationTime < SymptomEndTime.Value)
            {
                return SymptomState.Symptomatic;
            }

            return SymptomState.PostSymptomatic;
        }
    }

    /// <summary>
    /// Unity-independent authority for legal disease-state transitions and half-open timeline semantics.
    /// </summary>
    internal sealed class DiseaseStateEngine
    {
        private readonly HashSet<uint> trackedCitizens = new HashSet<uint>();
        private readonly HashSet<uint> removedCitizens = new HashSet<uint>();
        private readonly Dictionary<uint, DiseaseCourse> courses = new Dictionary<uint, DiseaseCourse>();

        public int TrackedPopulationCount => trackedCitizens.Count - removedCitizens.Count;

        internal IEnumerable<DiseaseCourse> Courses => courses.Values;

        public int InitialSeedCount { get; private set; }

        public int SecondaryTransmissionCount { get; private set; }

        public void Reset()
        {
            trackedCitizens.Clear();
            removedCitizens.Clear();
            courses.Clear();
            InitialSeedCount = 0;
            SecondaryTransmissionCount = 0;
        }

        public void RegisterCitizen(uint citizenId)
        {
            if (citizenId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(citizenId));
            }

            trackedCitizens.Add(citizenId);
            removedCitizens.Remove(citizenId);
        }

        public bool RemoveCitizen(uint citizenId)
        {
            return trackedCitizens.Contains(citizenId) && removedCitizens.Add(citizenId);
        }

        public DiseaseState GetState(uint citizenId, DateTime simulationTime)
        {
            if (!trackedCitizens.Contains(citizenId) || removedCitizens.Contains(citizenId))
            {
                return DiseaseState.Removed;
            }

            if (!courses.TryGetValue(citizenId, out DiseaseCourse course))
            {
                return DiseaseState.Susceptible;
            }

            DiseaseState state = ResolveState(course, simulationTime);
            AdvanceToResolvedState(course, state);
            return state;
        }

        public bool IsSusceptible(uint citizenId, DateTime simulationTime)
        {
            return GetState(citizenId, simulationTime) == DiseaseState.Susceptible;
        }

        public bool IsInfectious(uint citizenId, DateTime simulationTime)
        {
            return courses.TryGetValue(citizenId, out DiseaseCourse course)
                && GetState(citizenId, simulationTime) == DiseaseState.Infectious
                && course.IsWithinInfectiousInterval(simulationTime);
        }

        public bool TryExpose(DiseaseCourse course)
        {
            return TryExpose(course, course?.ExposureTime ?? default(DateTime));
        }

        public bool TryExpose(DiseaseCourse course, DateTime simulationTime)
        {
            if (course == null)
            {
                throw new ArgumentNullException(nameof(course));
            }

            if (!trackedCitizens.Contains(course.CitizenId)
                || removedCitizens.Contains(course.CitizenId)
                || courses.ContainsKey(course.CitizenId))
            {
                return false;
            }

            if (simulationTime < course.ExposureTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTime),
                    "A disease course cannot be registered before its exposure time.");
            }

            course.CurrentDiseaseState = ResolveState(course, simulationTime);
            courses.Add(course.CitizenId, course);
            if (course.ExposureKind == DiseaseExposureKind.InitialSeed)
            {
                InitialSeedCount++;
            }
            else
            {
                SecondaryTransmissionCount++;
            }

            return true;
        }

        public bool TryGetCourse(uint citizenId, out DiseaseCourse course)
        {
            return courses.TryGetValue(citizenId, out course);
        }

        public bool TryMarkDead(uint citizenId, DateTime simulationTime)
        {
            if (!courses.TryGetValue(citizenId, out DiseaseCourse course))
            {
                return false;
            }

            DiseaseState state = GetState(citizenId, simulationTime);
            if (state != DiseaseState.Infectious && state != DiseaseState.PostInfectiousIll)
            {
                return false;
            }

            course.ScheduleDeath(simulationTime);
            course.CurrentDiseaseState = DiseaseState.Dead;
            return true;
        }

        public DiseaseStateCounts GetCounts(DateTime simulationTime)
        {
            var result = new DiseaseStateCounts();
            foreach (uint citizenId in trackedCitizens)
            {
                DiseaseState state = GetState(citizenId, simulationTime);
                switch (state)
                {
                    case DiseaseState.Susceptible:
                        result.Susceptible++;
                        break;
                    case DiseaseState.Exposed:
                        result.Exposed++;
                        break;
                    case DiseaseState.Infectious:
                        result.Infectious++;
                        break;
                    case DiseaseState.PostInfectiousIll:
                        result.PostInfectiousIll++;
                        break;
                    case DiseaseState.Recovered:
                        result.Recovered++;
                        break;
                    case DiseaseState.Dead:
                        result.Dead++;
                        break;
                    case DiseaseState.Removed:
                        result.Removed++;
                        break;
                }
            }

            return result;
        }

        public int GetSymptomaticCount(DateTime simulationTime)
        {
            int count = 0;
            foreach (KeyValuePair<uint, DiseaseCourse> pair in courses)
            {
                DiseaseState state = GetState(pair.Key, simulationTime);
                if (state != DiseaseState.Dead
                    && state != DiseaseState.Recovered
                    && state != DiseaseState.Removed
                    && pair.Value.GetSymptomState(simulationTime) == SymptomState.Symptomatic)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetCumulativeHospitalizationCount()
        {
            int count = 0;
            foreach (DiseaseCourse course in courses.Values)
            {
                if (course.HospitalizationState == HospitalizationState.Hospitalized
                    || course.HospitalizationState == HospitalizationState.Discharged)
                {
                    count++;
                }
            }

            return count;
        }

        public IList<DiseaseCourse> GetCourses()
        {
            return new List<DiseaseCourse>(courses.Values);
        }

        public IList<string> ValidateInvariants(DateTime simulationTime)
        {
            var errors = new List<string>();
            DiseaseStateCounts counts = GetCounts(simulationTime);
            int resolvedPopulation = counts.Susceptible
                + counts.Exposed
                + counts.Infectious
                + counts.PostInfectiousIll
                + counts.Recovered
                + counts.Dead;
            if (resolvedPopulation != TrackedPopulationCount)
            {
                errors.Add("Disease compartments do not equal the tracked epidemiological population.");
            }

            if (SecondaryTransmissionCount != CountCourses(DiseaseExposureKind.SecondaryTransmission))
            {
                errors.Add("Secondary transmission count does not equal successful non-seed S -> E transitions.");
            }

            if (InitialSeedCount != CountCourses(DiseaseExposureKind.InitialSeed))
            {
                errors.Add("Initial seed count does not equal successful seeded S -> E transitions.");
            }

            return errors;
        }

        private static DiseaseState ResolveState(DiseaseCourse course, DateTime simulationTime)
        {
            if (course.CurrentDiseaseState == DiseaseState.Dead)
            {
                return DiseaseState.Dead;
            }

            if (course.DeathTime.HasValue && simulationTime >= course.DeathTime.Value)
            {
                return DiseaseState.Dead;
            }

            if (simulationTime >= course.RecoveryTime)
            {
                return DiseaseState.Recovered;
            }

            if (simulationTime < course.InfectiousStartTime)
            {
                return DiseaseState.Exposed;
            }

            if (simulationTime < course.InfectiousEndTime)
            {
                return DiseaseState.Infectious;
            }

            return DiseaseState.PostInfectiousIll;
        }

        private static void EnsureLegalTransition(DiseaseState previous, DiseaseState next)
        {
            if (previous == next)
            {
                return;
            }

            bool legal = (previous == DiseaseState.Exposed && next == DiseaseState.Infectious)
                || (previous == DiseaseState.Infectious && (next == DiseaseState.PostInfectiousIll || next == DiseaseState.Recovered || next == DiseaseState.Dead))
                || (previous == DiseaseState.PostInfectiousIll && (next == DiseaseState.Recovered || next == DiseaseState.Dead));
            if (!legal)
            {
                throw new InvalidOperationException("Invalid disease-state transition: " + previous + " -> " + next + ".");
            }
        }

        private static void AdvanceToResolvedState(DiseaseCourse course, DiseaseState target)
        {
            while (course.CurrentDiseaseState != target)
            {
                DiseaseState next;
                switch (course.CurrentDiseaseState)
                {
                    case DiseaseState.Exposed:
                        next = DiseaseState.Infectious;
                        break;
                    case DiseaseState.Infectious:
                        if (target == DiseaseState.Dead
                            && course.DeathTime.HasValue
                            && course.DeathTime.Value < course.InfectiousEndTime)
                        {
                            next = DiseaseState.Dead;
                        }
                        else if (target == DiseaseState.Recovered
                            && course.RecoveryTime == course.InfectiousEndTime)
                        {
                            next = DiseaseState.Recovered;
                        }
                        else
                        {
                            next = DiseaseState.PostInfectiousIll;
                        }

                        break;
                    case DiseaseState.PostInfectiousIll:
                        next = target == DiseaseState.Dead ? DiseaseState.Dead : DiseaseState.Recovered;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "A terminal disease state cannot transition to " + target + ".");
                }

                EnsureLegalTransition(course.CurrentDiseaseState, next);
                course.CurrentDiseaseState = next;
            }
        }

        private int CountCourses(DiseaseExposureKind kind)
        {
            int count = 0;
            foreach (DiseaseCourse course in courses.Values)
            {
                if (course.ExposureKind == kind)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>A disjoint compartment count returned by <see cref="DiseaseStateEngine"/>.</summary>
    internal sealed class DiseaseStateCounts
    {
        public int Susceptible { get; set; }

        public int Exposed { get; set; }

        public int Infectious { get; set; }

        public int PostInfectiousIll { get; set; }

        public int Recovered { get; set; }

        public int Dead { get; set; }

        public int Removed { get; set; }
    }
}
