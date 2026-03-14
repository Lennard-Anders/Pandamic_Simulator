// <copyright file="ModPaths.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Core
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using ColossalFramework.Plugins;
    using SkyTools.Tools;

    /// <summary>Provides helper methods for resolving mod-related file system paths.</summary>
    internal static class ModPaths
    {
        private static string cachedModRoot;
        private static bool loggedResolutionFailure;

        /// <summary>
        /// Resolves the root directory of the current mod.
        /// </summary>
        /// <returns>A mod root path with a trailing directory separator, or an empty string if unresolved.</returns>
        public static string GetModRoot()
        {
            if (IsExistingDirectory(cachedModRoot))
            {
                return EnsureTrailingDirectorySeparator(cachedModRoot);
            }

            string resolvedPath = ResolveFromExecutingAssembly();
            if (!string.IsNullOrEmpty(resolvedPath))
            {
                cachedModRoot = resolvedPath;
                return cachedModRoot;
            }

            resolvedPath = ResolveFromPluginInfo(GetCurrentAssembly());
            if (!string.IsNullOrEmpty(resolvedPath))
            {
                cachedModRoot = resolvedPath;
                return cachedModRoot;
            }

            if (!loggedResolutionFailure)
            {
                loggedResolutionFailure = true;
                Log.Warning(
                    "The 'Real Time' mod could not resolve mod root path. "
                    + "Diagnostics: "
                    + GetResolutionDiagnostics());
            }

            return string.Empty;
        }

        /// <summary>
        /// Resolves the mod root by matching the loaded user mod instance.
        /// </summary>
        /// <param name="userModInstance">The user mod instance from Cities: Skylines API.</param>
        /// <returns>A mod root path with a trailing directory separator, or an empty string if unresolved.</returns>
        public static string GetModRootFromUserModInstance(object userModInstance)
        {
            if (userModInstance == null || PluginManager.instance == null)
            {
                return string.Empty;
            }

            foreach (var plugin in PluginManager.instance.GetPluginsInfo())
            {
                if (plugin == null)
                {
                    continue;
                }

                object pluginInstance = plugin.userModInstance;
                if (ReferenceEquals(pluginInstance, userModInstance)
                    || IsLikelySameUserModType(pluginInstance, userModInstance))
                {
                    string pluginPath = NormalizeDirectoryPath(plugin.modPath);
                    if (!string.IsNullOrEmpty(pluginPath))
                    {
                        cachedModRoot = pluginPath;
                        return cachedModRoot;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Tries to find plugin info for the current mod assembly.
        /// </summary>
        /// <returns>A plugin info object or <c>null</c> if unresolved.</returns>
        public static PluginManager.PluginInfo GetCurrentPluginInfo()
        {
            return GetPluginInfoForAssembly(GetCurrentAssembly());
        }

        private static string ResolveFromExecutingAssembly()
        {
            try
            {
                string assemblyLocation = GetCurrentAssembly().Location;
                if (string.IsNullOrEmpty(assemblyLocation))
                {
                    return string.Empty;
                }

                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                return NormalizeDirectoryPath(assemblyDirectory);
            }
            catch (Exception ex)
            {
                Log.Warning($"The 'Real Time' mod failed to resolve the root path from assembly location, error message: {ex}");
                return string.Empty;
            }
        }

        private static string ResolveFromPluginInfo(Assembly assembly)
        {
            var pluginInfo = GetPluginInfoForAssembly(assembly);
            if (pluginInfo != null)
            {
                return NormalizeDirectoryPath(pluginInfo.modPath);
            }

            return string.Empty;
        }

        private static PluginManager.PluginInfo GetPluginInfoForAssembly(Assembly assembly)
        {
            if (assembly == null || PluginManager.instance == null)
            {
                return null;
            }

            try
            {
                var directMatch = PluginManager.instance.FindPluginInfo(assembly);
                if (directMatch != null)
                {
                    return directMatch;
                }

                string assemblyPath = GetAssemblyPath(assembly);
                string assemblyFileName = !string.IsNullOrEmpty(assemblyPath) ? Path.GetFileName(assemblyPath) : string.Empty;

                foreach (var plugin in PluginManager.instance.GetPluginsInfo())
                {
                    if (plugin == null)
                    {
                        continue;
                    }

                    if (IsPluginInstanceForAssembly(plugin, assembly) || PluginContainsAssembly(plugin, assembly, assemblyPath))
                    {
                        return plugin;
                    }

                    if (!string.IsNullOrEmpty(assemblyFileName) && !string.IsNullOrEmpty(plugin.modPath))
                    {
                        string candidateAssemblyPath = Path.Combine(plugin.modPath, assemblyFileName);
                        if (File.Exists(candidateAssemblyPath))
                        {
                            return plugin;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"The 'Real Time' mod failed to find plugin info, error message: {ex}");
            }

            return null;
        }

        private static bool PluginContainsAssembly(PluginManager.PluginInfo plugin, Assembly targetAssembly, string targetAssemblyPath)
        {
            try
            {
                var pluginAssemblies = plugin.GetAssemblies();
                if (pluginAssemblies == null)
                {
                    return false;
                }

                foreach (var pluginAssembly in pluginAssemblies.Where(a => a != null))
                {
                    if (ReferenceEquals(pluginAssembly, targetAssembly))
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(targetAssemblyPath) && AreSamePath(GetAssemblyPath(pluginAssembly), targetAssemblyPath))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignored, caller will continue with other plugin checks.
            }

            return false;
        }

        private static bool IsPluginInstanceForAssembly(PluginManager.PluginInfo plugin, Assembly targetAssembly)
        {
            try
            {
                object userModInstance = plugin.userModInstance;
                return userModInstance != null && ReferenceEquals(userModInstance.GetType().Assembly, targetAssembly);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLikelySameUserModType(object pluginInstance, object expectedInstance)
        {
            if (pluginInstance == null || expectedInstance == null)
            {
                return false;
            }

            Type pluginType = pluginInstance.GetType();
            Type expectedType = expectedInstance.GetType();

            if (pluginType == expectedType)
            {
                return true;
            }

            return string.Equals(pluginType.FullName, expectedType.FullName, StringComparison.Ordinal)
                && string.Equals(pluginType.Assembly.GetName().Name, expectedType.Assembly.GetName().Name, StringComparison.Ordinal);
        }

        private static bool IsExistingDirectory(string path)
        {
            if (IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                string normalized = path;
                if (File.Exists(normalized))
                {
                    normalized = Path.GetDirectoryName(normalized);
                }

                if (IsNullOrWhiteSpace(normalized) || !Directory.Exists(normalized))
                {
                    return string.Empty;
                }

                return EnsureTrailingDirectorySeparator(Path.GetFullPath(normalized));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            char lastChar = path[path.Length - 1];
            if (lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar)
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        private static bool AreSamePath(string pathA, string pathB)
        {
            if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB))
            {
                return false;
            }

            try
            {
                string fullPathA = Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPathB = Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(fullPathA, fullPathB, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GetAssemblyPath(Assembly assembly)
        {
            if (assembly == null)
            {
                return string.Empty;
            }

            try
            {
                if (!string.IsNullOrEmpty(assembly.Location))
                {
                    return assembly.Location;
                }

                string codeBase = assembly.CodeBase;
                if (!string.IsNullOrEmpty(codeBase)
                    && Uri.TryCreate(codeBase, UriKind.Absolute, out var codeBaseUri)
                    && codeBaseUri.IsFile)
                {
                    return codeBaseUri.LocalPath;
                }

                string modulePath = assembly.ManifestModule?.FullyQualifiedName;
                if (!string.IsNullOrEmpty(modulePath)
                    && !string.Equals(modulePath, "<Unknown>", StringComparison.OrdinalIgnoreCase))
                {
                    return modulePath;
                }
            }
            catch
            {
                // Ignored, caller handles empty result.
            }

            return string.Empty;
        }

        private static Assembly GetCurrentAssembly() => Assembly.GetExecutingAssembly();

        private static string GetResolutionDiagnostics()
        {
            try
            {
                Assembly assembly = GetCurrentAssembly();
                string assemblyPath = GetAssemblyPath(assembly);
                var plugin = GetPluginInfoForAssembly(assembly);
                return $"Assembly='{assembly?.FullName}', AssemblyPath='{assemblyPath}', Plugin='{plugin?.name}', PluginPath='{plugin?.modPath}'";
            }
            catch (Exception ex)
            {
                return "diagnostics-failed: " + ex.GetType().Name;
            }
        }
    }
}
