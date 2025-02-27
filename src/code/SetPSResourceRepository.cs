// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.Globalization;
using System.Management.Automation;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceRepository cmdlet is used to set information for a repository.
    /// </summary>
    [Cmdlet(VerbsCommon.Set,
        "PSResourceRepository",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class SetPSResourceRepository : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";
        private const int DefaultPriority = -1;
        private Uri _url;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the name of the repository to be set.
        /// </sumamry>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the location of the repository to be set.
        /// </sumamry>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string URL { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = RepositoriesParameterSet)]
        [ValidateNotNullOrEmpty]
        public Hashtable[] Repositories { get; set; }

        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Trusted
        {
            get
            { return _trusted; }

            set
            {
                _trusted = value;
                isSet = true;
            }
        }
        private SwitchParameter _trusted;
        private bool isSet;

        /// <summary>
        /// Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched
        /// before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
        /// Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40).
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority { get; set; } = DefaultPriority;

        /// <summary>
        /// Specifies a hashtable of vault and secret names as Authentication information for the repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public Hashtable Authentication {get; set;}

        /// <summary>
        /// When specified, displays the successfully registered repository and its information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void ProcessRecord()
        {
            if (MyInvocation.BoundParameters.ContainsKey(nameof(URL)))
            {
                bool isUrlValid = Utils.TryCreateValidUrl(URL, this, out _url, out ErrorRecord errorRecord);
                if (!isUrlValid)
                {
                    ThrowTerminatingError(errorRecord);
                }
            }

            List<PSRepositoryInfo> items = new List<PSRepositoryInfo>();

            switch(ParameterSetName)
            {
                case NameParameterSet:
                    try
                    {
                        items.Add(UpdateRepositoryStoreHelper(Name, _url, Priority, Trusted, Authentication));
                    }
                    catch (Exception e)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorInNameParameterSet",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                    break;

                case RepositoriesParameterSet:
                    try
                    {
                        items = RepositoriesParameterSetHelper();
                    }
                    catch (Exception e)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorInRepositoriesParameterSet",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }

            if (PassThru)
            {
                foreach(PSRepositoryInfo item in items)
                {
                    WriteObject(item);
                }
            }
        }

        private PSRepositoryInfo UpdateRepositoryStoreHelper(string repoName, Uri repoUrl, int repoPriority, bool repoTrusted, Hashtable repoAuthentication)
        {
            if (repoUrl != null && !(repoUrl.Scheme == Uri.UriSchemeHttp || repoUrl.Scheme == Uri.UriSchemeHttps || repoUrl.Scheme == Uri.UriSchemeFtp || repoUrl.Scheme == Uri.UriSchemeFile))
            {
                throw new ArgumentException("Invalid url, must be one of the following Uri schemes: HTTPS, HTTP, FTP, File Based");
            }

            // check repoName can't contain * or just be whitespace
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim();
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                throw new ArgumentException("Name cannot be null/empty, contain asterisk or be just whitespace");
            }

            // check PSGallery URL is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoUrl != null)
            {
                throw new ArgumentException("The PSGallery repository has a pre-defined URL.  Setting the -URL parameter for this repository is not allowed, instead try running 'Register-PSResourceRepository -PSGallery'.");
            }

            // check PSGallery Authentication is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoAuthentication != null)
            {
                throw new ArgumentException("The PSGallery repository does not require authentication.  Setting the -Authentication parameter for this repository is not allowed, instead try running 'Register-PSResourceRepository -PSGallery'.");
            }

            // determine trusted value to pass in (true/false if set, null otherwise, hence the nullable bool variable)
            bool? _trustedNullable = isSet ? new bool?(repoTrusted) : new bool?();

            if (repoAuthentication != null)
            {
                 if (!repoAuthentication.ContainsKey(AuthenticationHelper.VaultNameAttribute) || string.IsNullOrEmpty(repoAuthentication[AuthenticationHelper.VaultNameAttribute].ToString())
                    || !repoAuthentication.ContainsKey(AuthenticationHelper.SecretAttribute) || string.IsNullOrEmpty(repoAuthentication[AuthenticationHelper.SecretAttribute].ToString()))
                {
                    throw new ArgumentException($"Invalid Authentication, must include {AuthenticationHelper.VaultNameAttribute} and {AuthenticationHelper.SecretAttribute} key/(non-empty) value pairs");
                }
            }

            // determine if either 1 of 4 values are attempting to be set: URL, Priority, Trusted, Authentication.
            // if none are (i.e only Name parameter was provided, write error)
            if(repoUrl == null && repoPriority == DefaultPriority && _trustedNullable == null && repoAuthentication == null)
            {
                throw new ArgumentException("Either URL, Priority, Trusted or Authentication parameters must be requested to be set");
            }

            WriteVerbose("All required values to set repository provided, calling internal Update() API now");
            if (!ShouldProcess(repoName, "Set repository's value(s) in repository store"))
            {
                return null;
            }
            return RepositorySettings.Update(repoName, repoUrl, repoPriority, _trustedNullable, repoAuthentication);
        }

        private List<PSRepositoryInfo> RepositoriesParameterSetHelper()
        {
            List<PSRepositoryInfo> reposUpdatedFromHashtable = new List<PSRepositoryInfo>();
            foreach (Hashtable repo in Repositories)
            {
                if (!repo.ContainsKey("Name") || repo["Name"] == null || String.IsNullOrEmpty(repo["Name"].ToString()))
                {
                    WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Repository hashtable must contain Name key value pair"),
                            "NullNameForRepositoriesParameterSetRepo",
                            ErrorCategory.InvalidArgument,
                            this));
                    continue;
                }

                PSRepositoryInfo parsedRepoAdded = RepoValidationHelper(repo);
                if (parsedRepoAdded != null)
                {
                    reposUpdatedFromHashtable.Add(parsedRepoAdded);
                }
            }
            return reposUpdatedFromHashtable;
        }

        private PSRepositoryInfo RepoValidationHelper(Hashtable repo)
        {
            WriteVerbose(String.Format("Parsing through repository: {0}", repo["Name"]));

            Uri repoURL = null;
            if (repo.ContainsKey("Url"))
            {
                if (String.IsNullOrEmpty(repo["Url"].ToString()))
                {
                    WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Repository url cannot be null if provided"),
                            "NullURLForRepositoriesParameterSetUpdate",
                            ErrorCategory.InvalidArgument,
                            this));
                    return null;
                }

                if (!Utils.TryCreateValidUrl(urlString: repo["Url"].ToString(),
                    cmdletPassedIn: this,
                    urlResult: out repoURL,
                    errorRecord: out ErrorRecord errorRecord))
                {
                    WriteError(errorRecord);
                    return null;
                }
            }

            bool repoTrusted = false;
            isSet = false;
            if(repo.ContainsKey("Trusted"))
            {
                repoTrusted = (bool) repo["Trusted"];
                isSet = true;
            }

            try
            {
                return UpdateRepositoryStoreHelper(repo["Name"].ToString(),
                    repoURL,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : DefaultPriority,
                    repoTrusted,
                    repo["Authentication"] as Hashtable);
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(
                        new PSInvalidOperationException(e.Message),
                        "ErrorSettingIndividualRepoFromRepositories",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }
        }

        #endregion
    }
}
