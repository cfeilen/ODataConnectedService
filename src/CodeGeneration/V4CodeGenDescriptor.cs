﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.OData.ConnectedService.Models;
using Microsoft.OData.ConnectedService.Templates;
using Microsoft.VisualStudio.ConnectedServices;

namespace Microsoft.OData.ConnectedService.CodeGeneration
{
    internal class V4CodeGenDescriptor : BaseCodeGenDescriptor
    {
        public V4CodeGenDescriptor(string metadataUri, ConnectedServiceHandlerContext context, Project project, IODataT4CodeGeneratorFactory codeGeneratorFactory)
            : base(metadataUri, context, project)
        {
            ClientNuGetPackageName = Common.Constants.V4ClientNuGetPackage;
            ClientDocUri = Common.Constants.V4DocUri;
            ServiceConfiguration = base.ServiceConfiguration as ServiceConfigurationV4;
            CodeGeneratorFactory = codeGeneratorFactory;
        }

        private IODataT4CodeGeneratorFactory CodeGeneratorFactory { get; set; }

        private new ServiceConfigurationV4 ServiceConfiguration { get; set; }

        public override async Task AddNugetPackagesAsync()
        {
            await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Information, "Adding Nuget Packages...");

            foreach (var nugetPackage in Common.Constants.V4NuGetPackages)
                await CheckAndInstallNuGetPackageAsync(Common.Constants.NuGetOnlineRepository, nugetPackage);

            await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Information, "Nuget Packages were installed.");
        }

        public override async Task AddGeneratedClientCodeAsync()
        {
            await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Information, "Generating Client Proxy ...");

            if (this.ServiceConfiguration.IncludeT4File)
            {
                await AddT4FileAsync();
            }
            else
            {
                await AddGeneratedCSharpCodeAsync();
            }
        }

        private async Task AddT4FileAsync()
        {
            string tempFile = Path.GetTempFileName();
            string t4Folder = Path.Combine(this.CurrentAssemblyPath, "Templates");

            using (StreamWriter writer = File.CreateText(tempFile))
            {
                string text = File.ReadAllText(Path.Combine(t4Folder, "ODataT4CodeGenerator.tt"));

                text = Regex.Replace(text, "ODataT4CodeGenerator(\\.ttinclude)", this.GeneratedFileNamePrefix + "$1");
                text = Regex.Replace(text, "(public const string MetadataDocumentUri = )\"\";", "$1\"" + ServiceConfiguration.Endpoint + "\";");
                text = Regex.Replace(text, "(public const bool UseDataServiceCollection = ).*;", "$1" + ServiceConfiguration.UseDataServiceCollection.ToString().ToLower(CultureInfo.InvariantCulture) + ";");
                text = Regex.Replace(text, "(public const string NamespacePrefix = )\"\\$rootnamespace\\$\";", "$1\"" + ServiceConfiguration.NamespacePrefix + "\";");
                text = Regex.Replace(text, "(public const string TargetLanguage = )\"OutputLanguage\";", "$1\"CSharp\";");
                text = Regex.Replace(text, "(public const bool EnableNamingAlias = )true;", "$1" + this.ServiceConfiguration.EnableNamingAlias.ToString().ToLower(CultureInfo.InvariantCulture) + ";");
                text = Regex.Replace(text, "(public const bool IgnoreUnexpectedElementsAndAttributes = )true;", "$1" + this.ServiceConfiguration.IgnoreUnexpectedElementsAndAttributes.ToString().ToLower(CultureInfo.InvariantCulture) + ";");
                text = Regex.Replace(text, "(public const bool MakeTypesInternal = )false;", "$1" + ServiceConfiguration.MakeTypesInternal.ToString().ToLower(CultureInfo.InvariantCulture) + ";");
                await writer.WriteAsync(text);
                await writer.FlushAsync();
            }

            string referenceFolder = GetReferenceFileFolder();
            await this.Context.HandlerHelper.AddFileAsync(Path.Combine(t4Folder, "ODataT4CodeGenerator.ttinclude"), Path.Combine(referenceFolder, this.GeneratedFileNamePrefix + ".ttinclude"));
            await this.Context.HandlerHelper.AddFileAsync(tempFile, Path.Combine(referenceFolder, this.GeneratedFileNamePrefix + ".tt"));
        }

        private async Task AddGeneratedCSharpCodeAsync()
        {
            ODataT4CodeGenerator t4CodeGenerator = CodeGeneratorFactory.Create();
            t4CodeGenerator.MetadataDocumentUri = MetadataUri;
            t4CodeGenerator.UseDataServiceCollection = this.ServiceConfiguration.UseDataServiceCollection;
            t4CodeGenerator.TargetLanguage = ODataT4CodeGenerator.LanguageOption.CSharp;
            t4CodeGenerator.IgnoreUnexpectedElementsAndAttributes = this.ServiceConfiguration.IgnoreUnexpectedElementsAndAttributes;
            t4CodeGenerator.EnableNamingAlias = this.ServiceConfiguration.EnableNamingAlias;
            t4CodeGenerator.NamespacePrefix = this.ServiceConfiguration.NamespacePrefix;
            t4CodeGenerator.MakeTypesInternal = ServiceConfiguration.MakeTypesInternal;

            string tempFile = Path.GetTempFileName();

            using (StreamWriter writer = File.CreateText(tempFile))
            {
                await writer.WriteAsync(t4CodeGenerator.TransformText());
                await writer.FlushAsync();
                if (t4CodeGenerator.Errors != null && t4CodeGenerator.Errors.Count > 0)
                {
                    foreach (var err in t4CodeGenerator.Errors)
                    {
                        await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Warning, err.ToString());
                    }
                }
            }
            string outputFile = Path.Combine(GetReferenceFileFolder(), this.GeneratedFileNamePrefix + ".cs");
            await this.Context.HandlerHelper.AddFileAsync(tempFile, outputFile, new AddFileOptions { OpenOnComplete = this.ServiceConfiguration.OpenGeneratedFilesInIDE });
        }
    }
}
