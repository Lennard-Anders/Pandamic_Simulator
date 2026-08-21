// <copyright file="SaveGameServices.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using ColossalFramework;
    using ColossalFramework.Packaging;
    using ColossalFramework.PlatformServices;

    /// <summary>One selectable save in the normal game save catalog.</summary>
    public sealed class SaveGameCatalogEntry
    {
        public string DisplayName { get; set; }

        public BaselineSaveIdentity Identity { get; set; }
    }

    /// <summary>Save catalog entries plus non-fatal per-save inspection warnings.</summary>
    public sealed class SaveGameCatalogSnapshot
    {
        public SaveGameCatalogSnapshot()
        {
            Entries = new List<SaveGameCatalogEntry>();
            Warnings = new List<string>();
        }

        public List<SaveGameCatalogEntry> Entries { get; private set; }

        public List<string> Warnings { get; private set; }
    }

    /// <summary>Resolved game objects for a fingerprint-verified baseline save.</summary>
    public sealed class BaselineSaveResolution
    {
        public bool Success { get; internal set; }

        public string Error { get; internal set; }

        public Package.Asset MetadataAsset { get; internal set; }

        public Package.Asset DataAsset { get; internal set; }

        public SaveGameMetaData Metadata { get; internal set; }
    }

    /// <summary>Lists and strictly resolves normal savegame package assets.</summary>
    public interface ISaveGameCatalogService
    {
        SaveGameCatalogSnapshot GetAvailableSaves();

        BaselineSaveResolution Resolve(BaselineSaveIdentity identity);
    }

    /// <summary>Requests a normal asynchronous game load for an exact baseline save.</summary>
    public interface ISaveGameReloadService
    {
        bool TryRequestReload(BaselineSaveIdentity identity, out string error);
    }

    /// <summary>Game-backed implementation of the baseline save catalog.</summary>
    public sealed class SaveGameCatalogService : ISaveGameCatalogService
    {
        public SaveGameCatalogSnapshot GetAvailableSaves()
        {
            SaveGameCatalogSnapshot result = new SaveGameCatalogSnapshot();
            foreach (Package.Asset asset in PackageManager.FilterAssets(new[] { UserAssetType.SaveGameMetaData }))
            {
                if (asset == null || !asset.isEnabled)
                {
                    continue;
                }

                try
                {
                    SaveGameMetaData metadata = asset.Instantiate<SaveGameMetaData>();
                    if (metadata == null || metadata.assetRef == null)
                    {
                        result.Warnings.Add("Skipped save metadata without a data asset: " + asset.fullName);
                        continue;
                    }

                    BaselineSaveIdentity identity = CreateIdentity(asset, metadata);
                    result.Entries.Add(new SaveGameCatalogEntry
                    {
                        DisplayName = string.IsNullOrEmpty(metadata.cityName) ? asset.name : metadata.cityName,
                        Identity = identity,
                    });
                }
                catch (Exception exception)
                {
                    result.Warnings.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Could not inspect save '{0}': {1}",
                        asset.fullName,
                        exception.Message));
                }
            }

            result.Entries.Sort(CompareEntries);
            return result;
        }

        public BaselineSaveResolution Resolve(BaselineSaveIdentity identity)
        {
            if (identity == null)
            {
                return Failure("The baseline save identity is missing.");
            }

            if (identity.SchemaVersion != ExperimentSchema.CurrentVersion)
            {
                return Failure("The baseline save identity schema is unsupported.");
            }

            Package.Asset asset = PackageManager.FindAssetByName(identity.AssetFullName, UserAssetType.SaveGameMetaData);
            if (asset == null)
            {
                return Failure("The selected baseline save is no longer present: " + identity.AssetFullName);
            }

            if (!asset.isEnabled)
            {
                return Failure("The selected baseline save is disabled: " + identity.AssetFullName);
            }

            try
            {
                SaveGameMetaData metadata = asset.Instantiate<SaveGameMetaData>();
                if (metadata == null || metadata.assetRef == null)
                {
                    return Failure("The selected baseline has invalid save metadata or no data asset.");
                }

                BaselineSaveIdentity actual = CreateIdentity(asset, metadata);
                string mismatch = FindMismatch(identity, actual);
                if (mismatch != null)
                {
                    return Failure("The selected baseline changed since it was chosen (" + mismatch + ").");
                }

                return new BaselineSaveResolution
                {
                    Success = true,
                    MetadataAsset = asset,
                    DataAsset = metadata.assetRef,
                    Metadata = metadata,
                };
            }
            catch (Exception exception)
            {
                return Failure("The selected baseline could not be verified: " + exception.Message);
            }
        }

        private static BaselineSaveIdentity CreateIdentity(Package.Asset asset, SaveGameMetaData metadata)
        {
            Package package = asset.package;
            Package.Asset dataAsset = metadata.assetRef;
            string packagePath = package == null ? null : package.packagePath;
            FileInfo packageFile = !string.IsNullOrEmpty(packagePath) && File.Exists(packagePath)
                ? new FileInfo(packagePath)
                : null;

            return new BaselineSaveIdentity
            {
                AssetFullName = asset.fullName,
                AssetName = asset.name,
                AssetChecksum = asset.checksum,
                AssetSize = asset.size,
                AssetType = asset.type.ToString(),
                AssetEnabled = asset.isEnabled,
                AssetUpdatedUtc = ToIsoUtc(asset.updateTime),
                AssetDataTimestampUtc = ToIsoUtc(asset.dataTimestamp),
                PackageName = package == null ? null : package.packageName,
                PackagePath = packagePath,
                PackageFormatVersion = package == null ? 0 : package.version,
                PackageVersion = package == null ? 0L : package.packageVersion,
                PublishedFileId = GetPublishedFileId(package),
                CityName = metadata.cityName,
                SaveTimestampUtc = ToIsoUtc(metadata.timeStamp),
                Environment = metadata.environment,
                MapThemeAssetFullName = metadata.mapThemeRef,
                AchievementsDisabled = metadata.achievementsDisabled,
                DataAssetFullName = dataAsset.fullName,
                DataAssetName = dataAsset.name,
                DataAssetType = dataAsset.type.ToString(),
                DataAssetChecksum = dataAsset.checksum,
                DataAssetSize = dataAsset.size,
                DataAssetUpdatedUtc = ToIsoUtc(dataAsset.updateTime),
                DataAssetDataTimestampUtc = ToIsoUtc(dataAsset.dataTimestamp),
                LocalFileSha256 = packageFile == null ? null : ComputeSha256(packageFile.FullName),
                LocalFileLength = packageFile == null ? 0L : packageFile.Length,
                LocalFileLastWriteUtc = packageFile == null ? null : ToIsoUtc(packageFile.LastWriteTimeUtc),
            };
        }

        private static string FindMismatch(BaselineSaveIdentity expected, BaselineSaveIdentity actual)
        {
            if (!Equal(expected.AssetFullName, actual.AssetFullName))
            {
                return "metadata asset name";
            }

            if (!Equal(expected.AssetName, actual.AssetName)
                || !Equal(expected.AssetUpdatedUtc, actual.AssetUpdatedUtc)
                || !Equal(expected.AssetDataTimestampUtc, actual.AssetDataTimestampUtc))
            {
                return "metadata asset name or timestamps";
            }

            if (!EqualIgnoreCase(expected.AssetChecksum, actual.AssetChecksum) || expected.AssetSize != actual.AssetSize)
            {
                return "metadata asset checksum or size";
            }

            if (!Equal(expected.AssetType, actual.AssetType)
                || expected.AssetEnabled != actual.AssetEnabled
                || !actual.AssetEnabled)
            {
                return "metadata asset type or enabled state";
            }

            if (!Equal(expected.PackageName, actual.PackageName)
                || expected.PackageFormatVersion != actual.PackageFormatVersion
                || expected.PackageVersion != actual.PackageVersion
                || expected.PublishedFileId != actual.PublishedFileId)
            {
                return "package identity";
            }

            if (!string.IsNullOrEmpty(expected.PackagePath) && !PathsEqual(expected.PackagePath, actual.PackagePath))
            {
                return "package path";
            }

            if (!Equal(expected.DataAssetFullName, actual.DataAssetFullName)
                || !Equal(expected.DataAssetName, actual.DataAssetName)
                || !EqualIgnoreCase(expected.DataAssetChecksum, actual.DataAssetChecksum)
                || expected.DataAssetSize != actual.DataAssetSize
                || !Equal(expected.DataAssetType, actual.DataAssetType)
                || !Equal(expected.DataAssetUpdatedUtc, actual.DataAssetUpdatedUtc)
                || !Equal(expected.DataAssetDataTimestampUtc, actual.DataAssetDataTimestampUtc))
            {
                return "save data asset identity";
            }

            if (!Equal(expected.CityName, actual.CityName)
                || !Equal(expected.SaveTimestampUtc, actual.SaveTimestampUtc)
                || !Equal(expected.Environment, actual.Environment)
                || !Equal(expected.MapThemeAssetFullName, actual.MapThemeAssetFullName)
                || expected.AchievementsDisabled != actual.AchievementsDisabled)
            {
                return "city metadata, environment, map theme, or achievement state";
            }

            if (!string.IsNullOrEmpty(expected.LocalFileSha256)
                && !EqualIgnoreCase(expected.LocalFileSha256, actual.LocalFileSha256))
            {
                return "local package SHA-256";
            }

            if (expected.LocalFileLength != 0L && expected.LocalFileLength != actual.LocalFileLength)
            {
                return "local package length";
            }

            if (!string.IsNullOrEmpty(expected.LocalFileLastWriteUtc)
                && !Equal(expected.LocalFileLastWriteUtc, actual.LocalFileLastWriteUtc))
            {
                return "local package modification time";
            }

            return null;
        }

        private static BaselineSaveResolution Failure(string error)
        {
            return new BaselineSaveResolution { Error = error };
        }

        private static int CompareEntries(SaveGameCatalogEntry left, SaveGameCatalogEntry right)
        {
            int city = string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            if (city != 0)
            {
                return city;
            }

            return string.Compare(right.Identity.SaveTimestampUtc, left.Identity.SaveTimestampUtc, StringComparison.Ordinal);
        }

        private static bool Equal(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool EqualIgnoreCase(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return Equal(left, right);
            }
        }

        private static string ToIsoUtc(DateTime value)
        {
            return value == default(DateTime) ? null : value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static ulong GetPublishedFileId(Package package)
        {
            return package == null ? 0UL : package.GetPublishedFileID().AsUInt64;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }

    /// <summary>Uses the game's normal load path after strict baseline resolution.</summary>
    public sealed class SaveGameReloadService : ISaveGameReloadService
    {
        private readonly ISaveGameCatalogService catalog;

        public SaveGameReloadService(ISaveGameCatalogService catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            this.catalog = catalog;
        }

        public bool TryRequestReload(BaselineSaveIdentity identity, out string error)
        {
            if (!Singleton<LoadingManager>.exists)
            {
                error = "The game's loading manager is unavailable.";
                return false;
            }

            LoadingManager loadingManager = Singleton<LoadingManager>.instance;
            if (SavePanel.isSaving)
            {
                error = "A save operation is in progress.";
                return false;
            }

            if (loadingManager.m_currentlyLoading)
            {
                error = "A level load is already in progress.";
                return false;
            }

            if (loadingManager.m_applicationQuitting)
            {
                error = "The application is quitting.";
                return false;
            }

            BaselineSaveResolution resolution = catalog.Resolve(identity);
            if (!resolution.Success)
            {
                error = resolution.Error;
                return false;
            }

            try
            {
                SaveGameMetaData metadata = resolution.Metadata;
                SimulationMetaData loadMetadata = new SimulationMetaData
                {
                    m_CityName = metadata.cityName,
                    m_updateMode = SimulationManager.UpdateMode.LoadGame,
                };

                Package package = resolution.DataAsset.package;
                if (metadata.achievementsDisabled
                    || (package != null && package.GetPublishedFileID() != PublishedFileId.invalid))
                {
                    loadMetadata.m_disableAchievements = SimulationMetaData.MetaBool.True;
                }

                if (!string.IsNullOrEmpty(metadata.mapThemeRef))
                {
                    Package.Asset mapThemeAsset = PackageManager.FindAssetByName(
                        metadata.mapThemeRef,
                        UserAssetType.MapThemeMetaData);
                    if (mapThemeAsset == null || !mapThemeAsset.isEnabled)
                    {
                        error = "The baseline map theme is missing or disabled: " + metadata.mapThemeRef;
                        return false;
                    }

                    loadMetadata.m_MapThemeMetaData = mapThemeAsset.Instantiate<MapThemeMetaData>();
                    if (loadMetadata.m_MapThemeMetaData == null)
                    {
                        error = "The baseline map theme could not be instantiated: " + metadata.mapThemeRef;
                        return false;
                    }

                    loadMetadata.m_MapThemeMetaData.SetSelfRef(mapThemeAsset);
                }

                // LoadLevel synchronously starts unloading; the caller must persist its reload token first.
                object request = loadingManager.LoadLevel(
                    resolution.DataAsset,
                    "Game",
                    "InGame",
                    loadMetadata,
                    false);
                if (request == null)
                {
                    error = "The game's loading manager rejected the baseline reload request.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "The baseline reload could not be requested: " + exception.Message;
                return false;
            }
        }
    }
}
