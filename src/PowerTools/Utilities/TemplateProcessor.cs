﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
namespace Microsoft.DbContextPackage.Utilities
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using EnvDTE;
    using Microsoft.DbContextPackage.Extensions;
    using Microsoft.DbContextPackage.Resources;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.TextTemplating;
    using Microsoft.VisualStudio.TextTemplating.VSHost;
    using System.Collections.Concurrent;

    internal class TemplateProcessor
    {
        private readonly Project _project;
        private readonly ConcurrentDictionary<string, string> _templateCache;

        public TemplateProcessor(Project project)
        {
            DebugCheck.NotNull(project);

            _project = project;
            _templateCache = new ConcurrentDictionary<string, string>();
        }

        public string Process(string templatePath, EfTextTemplateHost host)
        {
            DebugCheck.NotEmpty(templatePath);
            DebugCheck.NotNull(host);

            host.TemplateFile = templatePath;

            var output = GetEngine().ProcessTemplate(
                GetTemplate(templatePath),
                host);

            host.Errors.HandleErrors(Strings.ProcessTemplateError(Path.GetFileName(templatePath)));

            return output;
        }

        private string GetTemplate(string templatePath)
        {
            DebugCheck.NotEmpty(templatePath);

            string value;

            if (_templateCache.TryGetValue(templatePath, out value))
            {
                return value;
            }

            var items = templatePath.Split('\\');
            Debug.Assert(items.Length > 1);

            var childProjectItem
                = _project.ProjectItems
                    .GetItem(items[0]);

            for (int i = 1; childProjectItem != null && i < items.Length; i++)
            {
                var item = items[i];

                childProjectItem = childProjectItem.ProjectItems.GetItem(item);
            }

            string contents = null;

            if (childProjectItem != null)
            {
                var path = (string)childProjectItem.Properties.Item("FullPath").Value;

                if (!string.IsNullOrWhiteSpace(path))
                {
                    contents = File.ReadAllText(path);
                }
            }

            if (contents == null)
            {
                contents = Templates.GetDefaultTemplate(templatePath);
            }

            _templateCache.TryAdd(templatePath, contents);

            return contents;
        }

        private static ITextTemplatingEngine GetEngine()
        {
            var textTemplating = (ITextTemplatingComponents)Package.GetGlobalService(typeof(STextTemplating));

            return textTemplating.Engine;
        }
    }
}
