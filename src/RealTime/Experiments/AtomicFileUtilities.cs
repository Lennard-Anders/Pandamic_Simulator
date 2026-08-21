// <copyright file="AtomicFileUtilities.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.IO;

    /// <summary>Creates short sibling paths for atomic writes on the game's legacy Mono runtime.</summary>
    internal static class AtomicFileUtilities
    {
        private const int TemporaryIdentifierLength = 16;

        public static string CreateTemporarySiblingPath(string destinationPath)
        {
            if (string.IsNullOrEmpty(destinationPath))
            {
                throw new ArgumentException("A destination path is required.", "destinationPath");
            }

            string fullPath = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("The destination path must have a parent directory.", "destinationPath");
            }

            // Do not append the suffix to the destination filename. That exceeded MAX_PATH in
            // Cities: Skylines' legacy Mono runtime even when the final CSV itself was valid.
            return Path.Combine(
                directory,
                ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, TemporaryIdentifierLength));
        }
    }
}
