// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DbContextPackage.Utilities
{
    internal class ProjectFilesPathGenUtility
    {
        internal static void SyncDirectoryWithNamespace(string projectNamespace, string namespaceToCompare, string defaultNamespaceSuffix, string defaultDirectory, ref string syncDirectoryPath)
        {
            if (string.Equals(syncDirectoryPath, defaultDirectory))
            {
                if (!string.Equals(string.Concat(projectNamespace, ".", defaultNamespaceSuffix), namespaceToCompare, StringComparison.InvariantCultureIgnoreCase))
                {
                    syncDirectoryPath = Path.Combine(syncDirectoryPath, NamesspaceToDirectoryPath(namespaceToCompare, projectNamespace));
                }
                else
                {
                    syncDirectoryPath = Path.Combine(syncDirectoryPath, defaultNamespaceSuffix);
                }
            }
        }

        /// <summary>
        /// Method to handle the cases of nested namespaces. 
        /// </summary>
        /// <param name="namespaceToCompare">Namespace from the host file</param>
        /// <param name="projectNamespace">Project's default namespace</param>
        /// <returns>Nested namespace string representing directory structure </returns>
        private static string NamesspaceToDirectoryPath(string namespaceToCompare, string projectNamespace)
        {
            var namespaceArray = namespaceToCompare.Split('.');

            var nestedNamespace = namespaceArray.Except(projectNamespace.Split('.')).ToArray();

            if (nestedNamespace.Length <= namespaceArray.Length)
            {
                return string.Join("\\", nestedNamespace);
            }

            if (nestedNamespace == null && namespaceArray.Length > 1)
            {
                return string.Join("\\", namespaceArray);
            }

            return namespaceToCompare;
        }

    }
}
