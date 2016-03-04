using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.DbContextPackage.Utilities
{
    public class ProjectFilePathGenUtilityTests
    {
        /// <summary>
        /// Test case to verfiy and accomodate new changes 
        /// i.e. if efhost object have namespace changed from templates then directory should be created by that name.
        /// </summary>
        [Fact]
        public void SyncDirectoryWithNamespace_NameSpaceChangeDetected_UpdatesPathHierarchy()
        {
            // Setup
            string projectNamespace = "PowerTools.Tests";
            string namespaceToCompare = "DataAccess";
            string defaultNamespaceSuffix = "Models";
            string defaultDirectory = "C:\\Temp\\PowerToolTests";
            string syncDirectoryPath = "C:\\Temp\\PowerToolTests";

            // Expected
            string expectedPath = "C:\\Temp\\PowerToolTests\\DataAccess";

            ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                            projectNamespace,
                                            namespaceToCompare,
                                            defaultNamespaceSuffix,
                                            defaultDirectory,
                                            ref syncDirectoryPath);

            Assert.Equal(expectedPath, syncDirectoryPath);
        }

        /// <summary>
        /// Test case to verfiy and accomodate new changes 
        /// i.e. if efhost object have namespace changed from templates then directory should be created by that name.
        /// </summary>
        [Fact]
        public void SyncDirectoryWithNamespace_NewNestedNameSpaces_UpdatesPathHierarchy()
        {
            // Setup
            string projectNamespace = "PowerTools.Tests";
            string namespaceToCompare = "DAL.Models";
            string defaultNamespaceSuffix = "Models";
            string defaultDirectory = "C:\\Temp\\PowerToolTests";
            string syncDirectoryPath = "C:\\Temp\\PowerToolTests";

            // Expected
            string expectedPath = "C:\\Temp\\PowerToolTests\\DAL\\Models";

            ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                            projectNamespace,
                                            namespaceToCompare,
                                            defaultNamespaceSuffix,
                                            defaultDirectory,
                                            ref syncDirectoryPath);

            Assert.Equal(expectedPath, syncDirectoryPath);
        }

        /// <summary>
        /// Test case to verfiy existing functionality shall not be affected by new changes.
        /// </summary>
        [Fact]
        public void SyncDirectoryWithNamespace_NameSpaceAlreadyUpdated_ShouldNotUpdatePath()
        {
            // Setup
            string projectNamespace = "PowerTools.Tests";
            string namespaceToCompare = "PowerTools.Tests.Models.Mappings";
            string defaultNamespaceSuffix = "Mappings";
            string defaultDirectory = "C:\\Temp\\PowerToolTests";
            string syncDirectoryPath = "C:\\Temp\\PowerToolTests";

            // Expected
            string expectedPath = "C:\\Temp\\PowerToolTests\\Models\\Mappings";

            ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                            projectNamespace,
                                            namespaceToCompare,
                                            defaultNamespaceSuffix,
                                            defaultDirectory,
                                            ref syncDirectoryPath);

            // At second attempt no change should be detected.
            ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                projectNamespace,
                                namespaceToCompare,
                                defaultNamespaceSuffix,
                                defaultDirectory,
                                ref syncDirectoryPath);

            Assert.Equal(expectedPath, syncDirectoryPath);
        }

        /// <summary>
        /// Test case to verfiy existing functionality shall not be affected by new changes.
        /// </summary>
        [Fact]
        public void SyncDirectoryWithNamespace_NoChangeInNameSpace_DirectoryShouldNotChange()
        {
            // Setup
            string projectNamespace = "PowerTools.Tests";
            string namespaceToCompare = "PowerTools.Tests.Models";
            string defaultNamespaceSuffix = "Models";
            string defaultDirectory = "C:\\Temp\\PowerToolTests";
            string syncDirectoryPath = "C:\\Temp\\PowerToolTests";

            // Expected
            string expectedPath = "C:\\Temp\\PowerToolTests\\Models";

            ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                            projectNamespace,
                                            namespaceToCompare,
                                            defaultNamespaceSuffix,
                                            defaultDirectory,
                                            ref syncDirectoryPath);

            Assert.Equal(expectedPath, syncDirectoryPath);
        }
    }
}
