// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NuGet.Configuration
{
    internal class SearchTree
    {
        private readonly SearchNode _root;

        internal SearchTree(PackageSourceMapping configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _root = new SearchNode();

            foreach (KeyValuePair<string, IReadOnlyList<string>> patternsPerSource in configuration.Patterns)
            {
                foreach (string namespaceId in patternsPerSource.Value)
                {
                    Add(patternsPerSource.Key, namespaceId);
                }
            }
        }

        private void Add(string packageSourceKey, string packagePattern)
        {
            SearchNode currentNode = _root;

            if (string.IsNullOrWhiteSpace(packagePattern))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Empty_Or_WhiteSpaceOnly, nameof(packagePattern));
            }

            // To prevent from unwanted behaviour.
            if (packagePattern.Length > PackageSourceMapping.PackageIdMaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(packagePattern));
            }

            packageSourceKey = packageSourceKey.Trim();
            packagePattern = packagePattern.ToLower(CultureInfo.CurrentCulture).Trim();

            for (int i = 0; i < packagePattern.Length; i++)
            {
                char c = packagePattern[i];

                if (c == '*')
                {
                    // break here since seeing * means end of expression.
                    currentNode.IsGlobbing = true;
                    break;
                }

                if (!currentNode.Children.ContainsKey(c))
                {
                    currentNode.Children[c] = new SearchNode();
                }

                currentNode = currentNode.Children[c];
            }

            if (currentNode.PackageSources == null)
            {
                currentNode.PackageSources = new List<string>();
            }

            currentNode.PackageSources.Add(packageSourceKey);
        }

        /// <summary>
        /// Finds the pattern in <see cref="PackageSourceMapping.Patterns"/> which satisfies the <paramref name="term"/>.
        /// Patterns are all lowercase and include glob (`*`) characters.
        /// </summary>
        /// <param name="term">Term to search for in <see cref="PackageSourceMapping.Patterns"/>.</param>
        /// <returns>Found <see cref="PackageSourceMapping.Patterns"/> which satisfies the <paramref name="term"/>, or `null`.</returns>
        public string? SearchForPattern(string term)
        {
            return SearchPatternByTerm(term);
        }

        /// <summary>
        /// Get package source names with matching prefix "term" from package source mapping section.
        /// </summary>
        /// <param name="term">Search term. Cannot be null, empty, or whitespace only. </param>
        /// <returns>Package source names with matching prefix "term" from package source mapping section.</returns>
        /// <exception cref="ArgumentException"> if <paramref name="term"/> is null, empty, or whitespace only.</exception>
        public IReadOnlyList<string> GetConfiguredPackageSources(string term)
        {
            SearchNode? searchNodeResult = SearchNodeByTerm(term);
            if (searchNodeResult is null || searchNodeResult.PackageSources is null)
            {
                return Array.Empty<string>();
            }

            return searchNodeResult.PackageSources;
        }

        private SearchNode? SearchNodeByTerm(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Empty_Or_WhiteSpaceOnly, nameof(term));
            }

            term = term.ToLower(CultureInfo.CurrentCulture).Trim();

            SearchNode currentNode = _root;
            SearchNode? longestMatchingPrefixNode = null;

            if (currentNode.IsGlobbing)
            {
                longestMatchingPrefixNode = currentNode;
            }

            int i = 0;

            for (; i < term.Length; i++)
            {
                char c = term[i];

                if (!currentNode.Children.TryGetValue(c, out SearchNode? child))
                {
                    break;
                }

                currentNode = child;

                if (currentNode.IsGlobbing)
                {
                    longestMatchingPrefixNode = currentNode;
                }
            }

            if (i == term.Length && currentNode.PackageSources != null)
            {
                return currentNode;
            }

            return longestMatchingPrefixNode;
        }

        private string? SearchPatternByTerm(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Empty_Or_WhiteSpaceOnly, nameof(term));
            }

            term = term.ToLower(CultureInfo.CurrentCulture).Trim();

            StringBuilder sb = new StringBuilder();
            SearchNode currentNode = _root;

            if (currentNode.IsGlobbing)
            {
                return "*";
            }

            int i = 0;

            for (; i < term.Length; i++)
            {
                char c = term[i];

                if (!currentNode.Children.TryGetValue(c, out SearchNode? child))
                {
                    if (!currentNode.IsGlobbing)
                    {
                        return null;
                    }
                    break;
                }

                currentNode = child;

                sb.Append(c);
            }
            if (currentNode.IsGlobbing)
            {
                sb.Append('*');
            }

            return sb.ToString();
        }
    }
}
