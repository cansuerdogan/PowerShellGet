﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml.Linq;
using static System.Environment;
using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// The class contains basic information of a repository path settings as well as methods to
    /// perform CRUD operations on the repository store file.
    /// </summary>

    internal static class RepositorySettings
    {
        /// <summary>
        /// File name for a user's repository store file is 'PSResourceRepository.xml'
        /// The repository store file's location is currently only at '%LOCALAPPDATA%\PowerShellGet' for the user account.
        /// </summary>
        private const string PSGalleryRepoName = "PSGallery";
        private const string PSGalleryRepoURL = "https://www.powershellgallery.com/api/v2";
        private const int defaultPriority = 50;
        private const bool defaultTrusted = false;
        private const string RepositoryFileName = "PSResourceRepository.xml";
        private static readonly string RepositoryPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet");
        private static readonly string FullRepositoryPath = Path.Combine(RepositoryPath, RepositoryFileName);

        private static readonly string VaultNameAttribute = "VaultName";
        private static readonly string SecretAttribute = "Secret";

        /// <summary>
        /// Check if repository store xml file exists, if not then create
        /// </summary>
        public static void CheckRepositoryStore()
        {
            if (!File.Exists(FullRepositoryPath))
            {
                try
                {
                    if (!Directory.Exists(RepositoryPath))
                    {
                        Directory.CreateDirectory(RepositoryPath);
                    }

                    XDocument newRepoXML = new XDocument(
                            new XElement("configuration")
                    );
                    newRepoXML.Save(FullRepositoryPath);
                }
                catch (Exception e)
                {
                    throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store creation failed with error: {0}.", e.Message));
                }

                // Add PSGallery to the newly created store
                Uri psGalleryUri = new Uri(PSGalleryRepoURL);
                Add(PSGalleryRepoName, psGalleryUri, defaultPriority, defaultTrusted, null);
            }

            // Open file (which should exist now), if cannot/is corrupted then throw error
            try
            {
                XDocument.Load(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store may be corrupted, file reading failed with error: {0}.", e.Message));
            }
        }

        /// <summary>
        /// Add a repository to the store
        /// Returns: PSRepositoryInfo containing information about the repository just added to the repository store
        /// </summary>
        /// <param name="sectionName"></param>
        public static PSRepositoryInfo Add(string repoName, Uri repoURL, int repoPriority, bool repoTrusted, Hashtable repoAuthentication)
        {
            Dbg.Assert(!string.IsNullOrEmpty(repoName), "Repository name cannot be null or empty");
            Dbg.Assert(!string.IsNullOrEmpty(repoURL.ToString()), "Repository URL cannot be null or empty");

            try
            {
                // Open file
                XDocument doc = XDocument.Load(FullRepositoryPath);
                if (FindRepositoryElement(doc, repoName) != null)
                {
                    throw new PSInvalidOperationException(String.Format("The PSResource Repository '{0}' already exists.", repoName));
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // Create new element
                XElement newElement = new XElement(
                    "Repository",
                    new XAttribute("Name", repoName),
                    new XAttribute("Url", repoURL),
                    new XAttribute("Priority", repoPriority),
                    new XAttribute("Trusted", repoTrusted)
                    );

                if(repoAuthentication != null) {
                    Dbg.Assert(repoAuthentication.ContainsKey(VaultNameAttribute), "Authentication has to contain Vault Name");
                    Dbg.Assert(!string.IsNullOrEmpty(repoAuthentication[VaultNameAttribute].ToString()), "Vault Name cannot be null or empty");
                    Dbg.Assert(repoAuthentication.ContainsKey(SecretAttribute), "Authentication has to contain Secret");
                    Dbg.Assert(!string.IsNullOrEmpty(repoAuthentication[SecretAttribute].ToString()), "Secret cannot be null or empty");

                    newElement.Add(new XAttribute(VaultNameAttribute, repoAuthentication[VaultNameAttribute]));
                    newElement.Add(new XAttribute(SecretAttribute, repoAuthentication[SecretAttribute]));
                }

                root.Add(newElement);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Adding to repository store failed: {0}", e.Message));
            }

            return new PSRepositoryInfo(repoName, repoURL, repoPriority, repoTrusted, repoAuthentication);
        }

        /// <summary>
        /// Updates a repository name, URL, priority, or installation policy
        /// Returns:  void
        /// </summary>
        public static PSRepositoryInfo Update(string repoName, Uri repoURL, int repoPriority, bool? repoTrusted, Hashtable repoAuthentication)
        {
            Dbg.Assert(!string.IsNullOrEmpty(repoName), "Repository name cannot be null or empty");

            PSRepositoryInfo updatedRepo;
            try
            {
                // Open file
                XDocument doc = XDocument.Load(FullRepositoryPath);
                XElement node = FindRepositoryElement(doc, repoName);
                if (node == null)
                {
                    throw new ArgumentException("Cannot find the repository because it does not exist. Try registering the repository using 'Register-PSResourceRepository'");
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // A null URL value passed in signifies the URL was not attempted to be set.
                // So only set Url attribute if non-null value passed in for repoUrl
                if (repoURL != null)
                {
                    node.Attribute("Url").Value = repoURL.AbsoluteUri;
                }

                // A negative Priority value passed in signifies the Priority value was not attempted to be set.
                // So only set Priority attribute if non-null value passed in for repoPriority
                if (repoPriority >= 0)
                {
                    node.Attribute("Priority").Value = repoPriority.ToString();
                }

                // A null Trusted value passed in signifies the Trusted value was not attempted to be set.
                // So only set Trusted attribute if non-null value passed in for repoTrusted.
                if (repoTrusted != null)
                {
                    node.Attribute("Trusted").Value = repoTrusted.ToString();
                }

                // A null Authentication value passed in signifies that Authentication information was not attempted to be set.
                // So only set VaultName and Secret attributes if non-null value passed in for repoAuthentication
                if (repoAuthentication != null)
                {
                    Dbg.Assert(repoAuthentication.ContainsKey(VaultNameAttribute), "Authentication has to contain Vault Name");
                    Dbg.Assert(!string.IsNullOrEmpty(repoAuthentication[VaultNameAttribute].ToString()), "Vault Name cannot be null or empty");
                    Dbg.Assert(repoAuthentication.ContainsKey(SecretAttribute), "Authentication has to contain Secret");
                    Dbg.Assert(!string.IsNullOrEmpty(repoAuthentication[SecretAttribute].ToString()), "Secret cannot be null or empty");

                    if (node.Attribute(VaultNameAttribute) == null) {
                        node.Add(new XAttribute(VaultNameAttribute, repoAuthentication[VaultNameAttribute]));
                    }
                    else {
                        node.Attribute(VaultNameAttribute).Value = repoAuthentication[VaultNameAttribute].ToString();
                    }

                    if (node.Attribute(SecretAttribute) == null) {
                        node.Add(new XAttribute(SecretAttribute, repoAuthentication[SecretAttribute]));
                    }
                    else {
                        node.Attribute(SecretAttribute).Value = repoAuthentication[SecretAttribute].ToString();
                    }
                }

                // Create Uri from node Url attribute to create PSRepositoryInfo item to return.
                if (!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                {
                    throw new PSInvalidOperationException(String.Format("Unable to read incorrectly formatted URL for repo {0}", repoName));
                }

                // Create Authentication based on new values or whether it was empty to begin with
                Hashtable thisAuthentication = !string.IsNullOrEmpty(node.Attribute(VaultNameAttribute)?.Value) && !string.IsNullOrEmpty(node.Attribute(SecretAttribute)?.Value)
                    ? new Hashtable() {
                        { VaultNameAttribute, node.Attribute(VaultNameAttribute).Value },
                        { SecretAttribute, node.Attribute(SecretAttribute).Value }
                    }
                    : null;

                updatedRepo = new PSRepositoryInfo(repoName,
                    thisUrl,
                    Int32.Parse(node.Attribute("Priority").Value),
                    Boolean.Parse(node.Attribute("Trusted").Value),
                    thisAuthentication);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Updating to repository store failed: {0}", e.Message));
            }

            return updatedRepo;
        }

        /// <summary>
        /// Removes a repository from the XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public static void Remove(string[] repoNames, out string[] errorList)
        {
            List<string> tempErrorList = new List<string>();

            // Check to see if information we're trying to remove from the repository is valid
            if (repoNames == null || repoNames.Length == 0)
            {
                throw new ArgumentException("Repository name cannot be null or empty");
            }

            XDocument doc;
            try
            {
                // Open file
                doc = XDocument.Load(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            // Get root of XDocument (XElement)
            var root = doc.Root;

            foreach (string repo in repoNames)
            {
                XElement node = FindRepositoryElement(doc, repo);
                if (node == null)
                {
                    tempErrorList.Add(String.Format("Unable to find repository '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    continue;
                }

                // Remove item from file
                node.Remove();
            }

            // Close the file
            root.Save(FullRepositoryPath);
            errorList = tempErrorList.ToArray();
        }

        public static List<PSRepositoryInfo> Read(string[] repoNames, out string[] errorList)
        {
            List<string> tempErrorList = new List<string>();
            var foundRepos = new List<PSRepositoryInfo>();

            XDocument doc;
            try
            {
                // Open file
                doc = XDocument.Load(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            if (repoNames == null || !repoNames.Any() || string.Equals(repoNames[0], "*") || repoNames[0] == null)
            {
                // Name array or single value is null so we will list all repositories registered
                // iterate through the doc
                foreach (XElement repo in doc.Descendants("Repository"))
                {
                    if (!Uri.TryCreate(repo.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                    {
                        tempErrorList.Add(String.Format("Unable to read incorrectly formatted URL for repo {0}", repo.Attribute("Name").Value));
                        continue;
                    }

                    Hashtable thisAuthentication = null;
                    string authErrorMessage = $"Repository {repo.Attribute("Name")} has invalid Authentication information. {VaultNameAttribute} and {SecretAttribute} should both be present and non-empty";
                    // both keys present
                    if (repo.Attribute(VaultNameAttribute) != null && repo.Attribute(SecretAttribute) != null) {
                        // both values non-empty
                        // = valid authentication
                        if (!string.IsNullOrEmpty(repo.Attribute(VaultNameAttribute).Value) && !string.IsNullOrEmpty(repo.Attribute(SecretAttribute).Value)) {
                            thisAuthentication = new Hashtable() {
                                { VaultNameAttribute, repo.Attribute(VaultNameAttribute).Value },
                                { SecretAttribute, repo.Attribute(SecretAttribute).Value }
                            };
                        }
                        else {
                            tempErrorList.Add(authErrorMessage);
                            continue;
                        }
                    }
                    // both keys are missing
                    else if (repo.Attribute(VaultNameAttribute) == null && repo.Attribute(SecretAttribute) == null) {
                        // = valid authentication, do nothing
                    }
                    // one of the keys is missing
                    else {
                        tempErrorList.Add(authErrorMessage);
                        continue;
                    }

                    PSRepositoryInfo currentRepoItem = new PSRepositoryInfo(repo.Attribute("Name").Value,
                        thisUrl,
                        Int32.Parse(repo.Attribute("Priority").Value),
                        Boolean.Parse(repo.Attribute("Trusted").Value),
                        thisAuthentication);

                    foundRepos.Add(currentRepoItem);
                }
            }
            else
            {
                foreach (string repo in repoNames)
                {
                    bool repoMatch = false;
                    WildcardPattern nameWildCardPattern = new WildcardPattern(repo, WildcardOptions.IgnoreCase);

                    foreach (var node in doc.Descendants("Repository").Where(e => nameWildCardPattern.IsMatch(e.Attribute("Name").Value)))
                    {
                        repoMatch = true;
                        if (!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                        {
                            //debug statement
                            tempErrorList.Add(String.Format("Unable to read incorrectly formatted URL for repo {0}", node.Attribute("Name").Value));
                            continue;
                        }

                        Hashtable thisAuthentication = null;
                        string authErrorMessage = $"Repository {node.Attribute("Name")} has invalid Authentication information. {VaultNameAttribute} and {SecretAttribute} should both be present and non-empty";
                        // both keys present
                        if (node.Attribute(VaultNameAttribute) != null && node.Attribute(SecretAttribute) != null) {
                            // both values non-empty
                            // = valid authentication
                            if (!string.IsNullOrEmpty(node.Attribute(VaultNameAttribute).Value) && !string.IsNullOrEmpty(node.Attribute(SecretAttribute).Value)) {
                                thisAuthentication = new Hashtable() {
                                    { VaultNameAttribute, node.Attribute(VaultNameAttribute).Value },
                                    { SecretAttribute, node.Attribute(SecretAttribute).Value }
                                };
                            }
                            else {
                                tempErrorList.Add(authErrorMessage);
                                continue;
                            }
                        }
                        // both keys are missing
                        else if (node.Attribute(VaultNameAttribute) == null && node.Attribute(SecretAttribute) == null) {
                            // = valid authentication, do nothing
                        }
                        // one of the keys is missing
                        else {
                            tempErrorList.Add(authErrorMessage);
                            continue;
                        }

                        PSRepositoryInfo currentRepoItem = new PSRepositoryInfo(node.Attribute("Name").Value,
                            thisUrl,
                            Int32.Parse(node.Attribute("Priority").Value),
                            Boolean.Parse(node.Attribute("Trusted").Value),
                            thisAuthentication);

                        foundRepos.Add(currentRepoItem);
                    }

                    if (!repo.Contains("*") && !repoMatch)
                    {
                        tempErrorList.Add(String.Format("Unable to find repository with Name '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    }
                }
            }

            errorList = tempErrorList.ToArray();
            // Sort by priority, then by repo name
            var reposToReturn = foundRepos.OrderBy(x => x.Priority).ThenBy(x => x.Name);
            return reposToReturn.ToList();
        }

        private static XElement FindRepositoryElement(XDocument doc, string name)
        {
            return doc.Descendants("Repository").Where(
                e => string.Equals(
                    e.Attribute("Name").Value,
                    name,
                    StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }
    }
}
