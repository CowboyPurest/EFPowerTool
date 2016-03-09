// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
namespace Microsoft.DbContextPackage.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.Common;
    using System.Data.Entity.Design;
    using System.Data.Entity.Design.PluralizationServices;
    using System.Data.Metadata.Edm;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using Microsoft.DbContextPackage.Extensions;
    using Microsoft.DbContextPackage.Resources;
    using Microsoft.DbContextPackage.Utilities;
    using Microsoft.VisualStudio.Data.Core;
    using Microsoft.VisualStudio.Data.Services;
    using Microsoft.VisualStudio.Shell;
    using System.Windows;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using Project = EnvDTE.Project;


    internal class ReverseEngineerCodeFirstHandler
    {
        private static readonly IEnumerable<EntityStoreSchemaFilterEntry> _storeMetadataFilters = new[]
            {
                new EntityStoreSchemaFilterEntry(null, null, "EdmMetadata", EntityStoreSchemaFilterObjectTypes.Table, EntityStoreSchemaFilterEffect.Exclude),
                new EntityStoreSchemaFilterEntry(null, null, "__MigrationHistory", EntityStoreSchemaFilterObjectTypes.Table, EntityStoreSchemaFilterEffect.Exclude)
            };
        private readonly DbContextPackage _package;

        public ReverseEngineerCodeFirstHandler(DbContextPackage package)
        {
            DebugCheck.NotNull(package);

            _package = package;
        }

        public void ReverseEngineerCodeFirst(Project project)
        {
            DebugCheck.NotNull(project);

            try
            {
                Stopwatch watcher = new Stopwatch();
                watcher.Start();
                string connectionString = string.Empty;
                string providerInvariant = string.Empty;
                string databaseName = string.Empty;
                bool isNewConnectionString = false;

                // Show available connection string
                var existingConnections = GetConnectionstrings(project);

                Connections connectionDialog = new Connections(existingConnections.Select(x => x.Name));
                connectionDialog.ShowModal();
                if (connectionDialog.IsConnectionStringSelected)
                {
                    var selected = connectionDialog.SelectedConnectionString;

                    var selectedConnectionSetting = existingConnections.First(x => x.Name.Equals(selected));
                    providerInvariant = selectedConnectionSetting.ProviderName;
                    connectionString = selectedConnectionSetting.ConnectionString;
                    var dbConnection = DbProviderFactories.GetFactory(providerInvariant).CreateConnection();
                    dbConnection.ConnectionString = connectionString;
                    databaseName = dbConnection.Database;
                }
                else
                {
                    // Show dialog with SqlClient selected by default
                    var dialogFactory = _package.GetService<IVsDataConnectionDialogFactory>();
                    var dialog = dialogFactory.CreateConnectionDialog();
                    dialog.AddAllSources();
                    dialog.SelectedSource = new Guid("067ea0d9-ba62-43f7-9106-34930c60c528");
                    var dialogResult = dialog.ShowDialog(connect: true);

                    if (dialogResult != null)
                    {
                        // Find connection string and provider
                        var connection = (DbConnection)dialogResult.GetLockedProviderObject();
                        connectionString = connection.ConnectionString;
                        var providerManager = (IVsDataProviderManager)Package.GetGlobalService(typeof(IVsDataProviderManager));
                        IVsDataProvider dp;
                        providerManager.Providers.TryGetValue(dialogResult.Provider, out dp);
                        providerInvariant = (string)dp.GetProperty("InvariantName");
                        databaseName = connection.Database;
                        isNewConnectionString = true;
                    }
                    else
                    {
                        // User selected not to proceed by clicking cancel or closes window.
                        return;
                    }
                }

                // Load store schema
                _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_LoadingSchema;

                var storeGenerator = new EntityStoreSchemaGenerator(providerInvariant, connectionString, "dbo");
                storeGenerator.GenerateForeignKeyProperties = true;
                var errors = storeGenerator.GenerateStoreMetadata(_storeMetadataFilters).Where(e => e.Severity == EdmSchemaErrorSeverity.Error);
                errors.HandleErrors(Strings.ReverseEngineer_SchemaError);

                // Generate default mapping
                _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_GenerateMapping;
                var contextName = databaseName.Replace(" ", string.Empty).Replace(".", string.Empty) + "Context";
                var modelGenerator = new EntityModelSchemaGenerator(storeGenerator.EntityContainer, "DefaultNamespace", contextName);
                modelGenerator.PluralizationService = PluralizationService.CreateService(new CultureInfo("en"));
                modelGenerator.GenerateForeignKeyProperties = true;
                modelGenerator.GenerateMetadata();

                // Pull out info about types to be generated
                var entityTypes = modelGenerator.EdmItemCollection.OfType<EntityType>().ToArray();
                var mappings = new EdmMapping(modelGenerator, storeGenerator.StoreItemCollection);

                // Find the project to add the code to
                var vsProject = (VSLangProj.VSProject)project.Object;
                var projectDirectory = new FileInfo(project.FileName).Directory;
                var defaultProjectNameSpace = (string)project.Properties.Item("RootNamespace").Value;
                var references = vsProject.References.Cast<VSLangProj.Reference>();

                if (!references.Any(r => r.Name == "EntityFramework"))
                {
                    // Add EF References
                    _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_InstallEntityFramework;

                    try
                    {
                        project.InstallPackage("EntityFramework");
                    }
                    catch (Exception ex)
                    {
                        _package.LogError(Strings.ReverseEngineer_InstallEntityFrameworkError, ex);
                    }
                }

                // Generate Entity Classes and Mappings
                var templateProcessor = new TemplateProcessor(project);
                var modelsNamespaceSuffixDefault = "Models";
                var mappingNamespaceSuffixDefault = "Mappings";

                var projectNamespace = defaultProjectNameSpace;
                var modelsNamespace = string.Concat(projectNamespace, ".", modelsNamespaceSuffixDefault);
                var mappingNamespace = string.Concat(modelsNamespace, ".", mappingNamespaceSuffixDefault);

                var modelsDirectory = projectDirectory.FullName;
                var mappingDirectory = projectDirectory.FullName;
                var contextDirectory = projectDirectory.FullName;

                var entityFrameworkVersion = GetEntityFrameworkVersion(references);
                ConcurrentDictionary<string, string> models = new ConcurrentDictionary<string, string>();
                ConcurrentDictionary<string, string> maps = new ConcurrentDictionary<string, string>();

                // Process the templates and generate content
                Parallel.ForEach(entityTypes, (entityType) =>
                {
                    _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_GenerateClasses(entityType.Name);

                    var entityHost = new EfTextTemplateHost
                        {
                            EntityType = entityType,
                            EntityContainer = modelGenerator.EntityContainer,
                            Namespace = projectNamespace,
                            ModelsNamespace = modelsNamespace,
                            MappingNamespace = mappingNamespace,
                            EntityFrameworkVersion = entityFrameworkVersion,
                            TableSet = mappings.EntityMappings[entityType].Item1,
                            PropertyToColumnMappings = mappings.EntityMappings[entityType].Item2,
                            ManyToManyMappings = mappings.ManyToManyMappings
                        };


                    var entityContents = templateProcessor.Process(Templates.EntityTemplate, entityHost);

                    models.TryAdd(entityType.Name + entityHost.FileExtension, entityContents);

                    var mappingHost = new EfTextTemplateHost
                        {
                            EntityType = entityType,
                            EntityContainer = modelGenerator.EntityContainer,
                            Namespace = projectNamespace,
                            ModelsNamespace = modelsNamespace,
                            MappingNamespace = mappingNamespace,
                            EntityFrameworkVersion = entityFrameworkVersion,
                            TableSet = mappings.EntityMappings[entityType].Item1,
                            PropertyToColumnMappings = mappings.EntityMappings[entityType].Item2,
                            ManyToManyMappings = mappings.ManyToManyMappings
                        };

                    var mappingContents = templateProcessor.Process(Templates.MappingTemplate, mappingHost);

                    maps.TryAdd(entityType.Name + "Map" + mappingHost.FileExtension, mappingContents);
                });

                // Generate Context
                _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_GenerateContext;
                var contextHost = new EfTextTemplateHost
                    {
                        EntityContainer = modelGenerator.EntityContainer,
                        Namespace = projectNamespace,
                        ModelsNamespace = modelsNamespace,
                        MappingNamespace = mappingNamespace,
                        EntityFrameworkVersion = entityFrameworkVersion
                    };

                var contextContents = templateProcessor.Process(Templates.ContextTemplate, contextHost);

                ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                                    defaultProjectNameSpace,
                                                    contextHost.Namespace,
                                                    modelsNamespaceSuffixDefault,
                                                    projectDirectory.FullName,
                                                    ref contextDirectory);

                var contextFilePath = Path.Combine(contextDirectory, modelGenerator.EntityContainer.Name + contextHost.FileExtension);

                _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_AddingFiles;
                var contextItem = project.AddNewFile(contextFilePath, contextContents);

                // sync model directory
                ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                                    defaultProjectNameSpace,
                                                    contextHost.ModelsNamespace,
                                                    modelsNamespaceSuffixDefault,
                                                    projectDirectory.FullName,
                                                    ref modelsDirectory);

                // Add models
                Parallel.ForEach(models, (file) =>
                {
                    project.AddNewFile(Path.Combine(modelsDirectory, file.Key), file.Value);
                });

                // sync project mapping directory
                ProjectFilesPathGenUtility.SyncDirectoryWithNamespace(
                                                    defaultProjectNameSpace,
                                                    contextHost.MappingNamespace,
                                                    mappingNamespaceSuffixDefault,
                                                    projectDirectory.FullName,
                                                    ref mappingDirectory);

                // Add mappings
                Parallel.ForEach(maps, (file) =>
                {
                    project.AddNewFile(Path.Combine(mappingDirectory, file.Key), file.Value);
                });

                if (isNewConnectionString)
                {
                    AddConnectionStringToConfigFile(project, connectionString, providerInvariant, modelGenerator.EntityContainer.Name);
                }

                if (contextItem != null)
                {
                    // Open context class when done
                    _package.DTE2.ItemOperations.OpenFile(contextFilePath);
                }

                watcher.Stop();
                _package.DTE2.StatusBar.Text = Strings.ReverseEngineer_Complete((int)watcher.Elapsed.TotalSeconds);
            }
            catch (Exception exception)
            {
                _package.LogError(Strings.ReverseEngineer_Error, exception);
            }
        }

        private static Version GetEntityFrameworkVersion(IEnumerable<VSLangProj.Reference> references)
        {
            var entityFrameworkReference = references.FirstOrDefault(r => r.Name == "EntityFramework");

            if (entityFrameworkReference != null)
            {
                return new Version(entityFrameworkReference.Version);
            }

            return null;
        }

        private static void AddConnectionStringToConfigFile(Project project, string connectionString, string providerInvariant, string connectionStringName)
        {
            DebugCheck.NotNull(project);
            DebugCheck.NotEmpty(providerInvariant);
            DebugCheck.NotEmpty(connectionStringName);

            // Find App.config or Web.config
            var configFilePath = Path.Combine(
                project.GetProjectDir(),
                project.IsWebProject()
                    ? "Web.config"
                    : "App.config");

            // Either load up the existing file or create a blank file
            var config = ConfigurationManager.OpenMappedExeConfiguration(
                new ExeConfigurationFileMap { ExeConfigFilename = configFilePath },
                ConfigurationUserLevel.None);

            // Find or create the connectionStrings section
            var connectionStringSettings = config.ConnectionStrings
                .ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .FirstOrDefault(css => css.Name == connectionStringName);

            if (connectionStringSettings == null)
            {
                connectionStringSettings = new ConnectionStringSettings
                    {
                        Name = connectionStringName
                    };

                config.ConnectionStrings
                    .ConnectionStrings
                    .Add(connectionStringSettings);
            }

            // Add in the new connection string
            connectionStringSettings.ProviderName = providerInvariant;
            connectionStringSettings.ConnectionString = FixUpConnectionString(connectionString, providerInvariant);

            project.DTE.SourceControl.CheckOutItemIfNeeded(configFilePath);
            config.Save();

            // Add any new file to the project
            project.ProjectItems.AddFromFile(configFilePath);
        }

        private static IEnumerable<ConnectionStringSettings> GetConnectionstrings(Project project)
        {
            // Find App.config or Web.config
            var configFilePath = Path.Combine(
                project.GetProjectDir(),
                project.IsWebProject()
                    ? "Web.config"
                    : "App.config");

            // Either load up the existing file or create a blank file
            var config = ConfigurationManager.OpenMappedExeConfiguration(
                new ExeConfigurationFileMap { ExeConfigFilename = configFilePath },
                ConfigurationUserLevel.None);

            // Find or create the connectionStrings section
            var connectionStrings = config.ConnectionStrings
                .ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .Where(x => !x.Name.Equals("localsqlserver", StringComparison.InvariantCultureIgnoreCase));

            return connectionStrings;
        }

        private static string FixUpConnectionString(string connectionString, string providerName)
        {
            DebugCheck.NotEmpty(providerName);

            if (providerName != "System.Data.SqlClient")
            {
                return connectionString;
            }

            var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    MultipleActiveResultSets = true
                };
            builder.Remove("Pooling");

            return builder.ToString();
        }

        private static string GetDatabaseName(DbConnectionStringBuilder builder, params string[] aliases)
        {
            object dbName = null;
            foreach (var alias in aliases)
            {
                if (builder.TryGetValue(alias, out dbName))
                {
                    return (string)dbName;
                }
            }

            return null;
        }

        private class EdmMapping
        {
            public EdmMapping(EntityModelSchemaGenerator mcGenerator, StoreItemCollection store)
            {
                DebugCheck.NotNull(mcGenerator);
                DebugCheck.NotNull(store);

                // Pull mapping xml out
                var mappingDoc = new XmlDocument();
                var mappingXml = new StringBuilder();

                using (var textWriter = new StringWriter(mappingXml))
                {
                    mcGenerator.WriteStorageMapping(new XmlTextWriter(textWriter));
                }

                mappingDoc.LoadXml(mappingXml.ToString());

                var entitySets = mcGenerator.EntityContainer.BaseEntitySets.OfType<EntitySet>();
                var associationSets = mcGenerator.EntityContainer.BaseEntitySets.OfType<AssociationSet>();
                var tableSets = store.GetItems<EntityContainer>().Single().BaseEntitySets.OfType<EntitySet>();

                this.EntityMappings = BuildEntityMappings(mappingDoc, entitySets, tableSets);
                this.ManyToManyMappings = BuildManyToManyMappings(mappingDoc, associationSets, tableSets);
            }

            public Dictionary<EntityType, Tuple<EntitySet, Dictionary<EdmProperty, EdmProperty>>> EntityMappings { get; set; }

            public Dictionary<AssociationType, Tuple<EntitySet, Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>>> ManyToManyMappings { get; set; }

            private static Dictionary<AssociationType, Tuple<EntitySet, Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>>> BuildManyToManyMappings(XmlDocument mappingDoc, IEnumerable<AssociationSet> associationSets, IEnumerable<EntitySet> tableSets)
            {
                DebugCheck.NotNull(mappingDoc);
                DebugCheck.NotNull(associationSets);
                DebugCheck.NotNull(tableSets);

                // Build mapping for each association
                var mappings = new Dictionary<AssociationType, Tuple<EntitySet, Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>>>();
                var namespaceManager = new XmlNamespaceManager(mappingDoc.NameTable);
                namespaceManager.AddNamespace("ef", mappingDoc.ChildNodes[0].NamespaceURI);
                foreach (var associationSet in associationSets.Where(a => !a.ElementType.AssociationEndMembers.Where(e => e.RelationshipMultiplicity != RelationshipMultiplicity.Many).Any()))
                {
                    var setMapping = mappingDoc.SelectSingleNode(string.Format("//ef:AssociationSetMapping[@Name=\"{0}\"]", associationSet.Name), namespaceManager);
                    var tableName = setMapping.Attributes["StoreEntitySet"].Value;
                    var tableSet = tableSets.Single(s => s.Name == tableName);

                    var endMappings = new Dictionary<RelationshipEndMember, Dictionary<EdmMember, string>>();
                    foreach (var end in associationSet.AssociationSetEnds)
                    {
                        var propertyToColumnMappings = new Dictionary<EdmMember, string>();
                        var endMapping = setMapping.SelectSingleNode(string.Format("./ef:EndProperty[@Name=\"{0}\"]", end.Name), namespaceManager);
                        foreach (XmlNode fk in endMapping.ChildNodes)
                        {
                            var propertyName = fk.Attributes["Name"].Value;
                            var property = end.EntitySet.ElementType.Properties[propertyName];
                            var columnName = fk.Attributes["ColumnName"].Value;
                            propertyToColumnMappings.Add(property, columnName);
                        }

                        endMappings.Add(end.CorrespondingAssociationEndMember, propertyToColumnMappings);
                    }

                    mappings.Add(associationSet.ElementType, Tuple.Create(tableSet, endMappings));
                }

                return mappings;
            }

            private static Dictionary<EntityType, Tuple<EntitySet, Dictionary<EdmProperty, EdmProperty>>> BuildEntityMappings(XmlDocument mappingDoc, IEnumerable<EntitySet> entitySets, IEnumerable<EntitySet> tableSets)
            {
                DebugCheck.NotNull(mappingDoc);
                DebugCheck.NotNull(entitySets);
                DebugCheck.NotNull(tableSets);

                // Build mapping for each type
                var mappings = new Dictionary<EntityType, Tuple<EntitySet, Dictionary<EdmProperty, EdmProperty>>>();
                var namespaceManager = new XmlNamespaceManager(mappingDoc.NameTable);
                namespaceManager.AddNamespace("ef", mappingDoc.ChildNodes[0].NamespaceURI);
                foreach (var entitySet in entitySets)
                {
                    // Post VS2010 builds use a different structure for mapping
                    var setMapping = mappingDoc.ChildNodes[0].NamespaceURI == "http://schemas.microsoft.com/ado/2009/11/mapping/cs"
                        ? mappingDoc.SelectSingleNode(string.Format("//ef:EntitySetMapping[@Name=\"{0}\"]/ef:EntityTypeMapping/ef:MappingFragment", entitySet.Name), namespaceManager)
                        : mappingDoc.SelectSingleNode(string.Format("//ef:EntitySetMapping[@Name=\"{0}\"]", entitySet.Name), namespaceManager);

                    var tableName = setMapping.Attributes["StoreEntitySet"].Value;
                    var tableSet = tableSets.Single(s => s.Name == tableName);

                    var propertyMappings = new Dictionary<EdmProperty, EdmProperty>();
                    foreach (var prop in entitySet.ElementType.Properties)
                    {
                        var propMapping = setMapping.SelectSingleNode(string.Format("./ef:ScalarProperty[@Name=\"{0}\"]", prop.Name), namespaceManager);
                        var columnName = propMapping.Attributes["ColumnName"].Value;
                        var columnProp = tableSet.ElementType.Properties[columnName];

                        propertyMappings.Add(prop, columnProp);
                    }

                    mappings.Add(entitySet.ElementType, Tuple.Create(tableSet, propertyMappings));
                }

                return mappings;
            }
        }
    }
}
