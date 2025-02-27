# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryURL = Get-PSGalleryLocation
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDir4Path = Join-Path -Path $TestDrive -ChildPath "tmpDir4"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path, $tmpDir4Path)
        Get-NewTestDirs($tmpDirPaths)

        $relativeCurrentPath = Get-Location
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDir4Path = Join-Path -Path $TestDrive -ChildPath "tmpDir4"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path, $tmpDir4Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "register repository given Name, URL (bare minimum for NameParmaterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with Name, URL, Trusted (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Trusted -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository given Name, URL, Trusted, Priority (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repository given Name, URL, Trusted, Priority, Authentication (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Trusted -Priority 20 -Authentication @{VaultName = "testvault"; Secret = "testsecret"} -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
        $res.Authentication["VaultName"] | Should -Be "testvault"
        $res.Authentication["Secret"] | Should -Be "testsecret"
    }

    It "register repository with PSGallery parameter (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted, Priority parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repositories with Repositories parameter, all name parameter style repositories (RepositoriesParameterSet)" {
        $hashtable1 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $hashtable2 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $hashtable3 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $hashtable4 = @{Name = "testRepository4"; URL = $tmpDir4Path; Trusted = $True; Priority = 30; Authentication = @{VaultName = "testvault"; Secret = "testsecret"}}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Register-PSResourceRepository -Repositories $arrayOfHashtables
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        $res2.URL.LocalPath | Should -Contain $tmpDir2Path
        $res2.Trusted | Should -Be True
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name "testRepository3"
        $res3.URL.LocalPath | Should -Contain $tmpDir3Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 20

        $res4 = Get-PSResourceRepository -Name "testRepository4"
        $res4.URL | Should -Contain $tmpDir4Path
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 30
        $res4.Authentication["VaultName"] | Should -Be "testvault"
        $res4.Authentication["Secret"] | Should -Be "testsecret"
    }

    It "register repositories with Repositories parameter, psgallery style repository (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        Register-PSResourceRepository -Repositories $hashtable1
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repositories with Repositories parameter, name and psgallery parameter styles (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        $hashtable2 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $hashtable3 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $hashtable4 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $hashtable5 = @{Name = "testRepository4"; URL = $tmpDir4Path; Trusted = $True; Priority = 30; Authentication = @{VaultName = "testvault"; Secret = "testsecret"}}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4, $hashtable5

        Register-PSResourceRepository -Repositories $arrayOfHashtables

        $res1 = Get-PSResourceRepository -Name $PSGalleryName
        $res1.URL | Should -Be $PSGalleryURL
        $res1.Trusted | Should -Be False
        $res1.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name "testRepository"
        $res2.URL.LocalPath | Should -Contain $tmpDir1Path
        $res2.Trusted | Should -Be False
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name "testRepository2"
        $res3.URL.LocalPath | Should -Contain $tmpDir2Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 50

        $res4 = Get-PSResourceRepository -Name "testRepository3"
        $res4.URL.LocalPath | Should -Contain $tmpDir3Path
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 20

        $res5 = Get-PSResourceRepository -Name "testRepository4"
        $res5.URL | Should -Contain $tmpDir4Path
        $res5.Trusted | Should -Be True
        $res5.Priority | Should -Be 30
        $res5.Authentication["VaultName"] | Should -Be "testvault"
        $res5.Authentication["Secret"] | Should -Be "testsecret"
    }

    It "not register repository when Name is provided but URL is not" {
        {Register-PSResourceRepository -Name "testRepository" -URL "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is empty but URL is provided" {
        {Register-PSResourceRepository -Name "" -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is null but URL is provided" {
        {Register-PSResourceRepository -Name $null -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is just whitespace but URL is provided" {
        {Register-PSResourceRepository -Name " " -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register if Authentication is missing VaultName or Secret" {
        {Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Authentication @{Secret = "test"} -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Authentication @{VaultName = "test"} -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register if Authentication values are empty" {
        {Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Authentication @{VaultName = "test"; Secret = ""} -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Authentication @{VaultName = ""; Secret = "test"} -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register PSGallery with NameParameterSet" {
        {Register-PSResourceRepository -Name $PSGalleryName -URL $PSGalleryURL -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    # this error message comes from the parameter cmdlet tags (earliest point of detection)
    It "not register PSGallery when PSGallery parameter provided with Name, URL or Authentication" {
        {Register-PSResourceRepository -PSGallery -Name $PSGalleryName -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -PSGallery -URL $PSGalleryURL -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -PSGallery -Authentication @{VaultName = "testvault"; Secret = "testsecret"} -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    $testCases = @{Type = "Name key specified with PSGallery key"; IncorrectHashTable = @{PSGallery = $True; Name=$PSGalleryName}},
                 @{Type = "URL key specified with PSGallery key";  IncorrectHashTable = @{PSGallery = $True; URL=$PSGalleryURL}},
                 @{Type = "Authentication key specified with PSGallery key";  IncorrectHashTable = @{PSGallery = $True; Authentication = @{VaultName = "test"; Secret = "test"}}}

    It "not register incorrectly formatted PSGallery type repo among correct ones when incorrect type is <Type>" -TestCases $testCases {
        param($Type, $IncorrectHashTable)

        $correctHashtable1 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $correctHashtable2 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $correctHashtable3 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3

        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NotProvideNameUrlAuthForPSGalleryRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"

        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        $res2.Name | Should -Be "testRepository2"

        $res3 = Get-PSResourceRepository -Name "testRepository3"
        $res3.Name | Should -Be "testRepository3"
    }

    $testCases2 = @{Type = "-Name is not specified";                    IncorrectHashTable = @{URL = $tmpDir1Path};                                                                              ErrorId = "NullNameForRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-Name is PSGallery";                        IncorrectHashTable = @{Name = "PSGallery"; URL = $tmpDir1Path};                                                          ErrorId = "PSGalleryProvidedAsNameRepoPSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-URL not specified";                        IncorrectHashTable = @{Name = "testRepository"};                                                                         ErrorId = "NullURLForRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-URL is not valid scheme";                  IncorrectHashTable = @{Name = "testRepository"; URL="www.google.com"};                                                   ErrorId = "InvalidUrl,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-Authentication is missing VaultName";      IncorrectHashTable = @{Name = "testRepository"; URL=$tmpDir1Path; Authentication = @{Secret = "test"}};                  ErrorId = "InvalidAuthentication,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-Authentication is missing Secret";         IncorrectHashTable = @{Name = "testRepository"; URL=$tmpDir1Path; Authentication = @{VaultName = "test"}};               ErrorId = "InvalidAuthentication,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-Authentication-VaultName value is empty";  IncorrectHashTable = @{Name = "testRepository"; URL=$tmpDir1Path; Authentication = @{VaultName = ""; Secret = "test"}};  ErrorId = "InvalidAuthentication,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-Authentication-Secret value is empty";     IncorrectHashTable = @{Name = "testRepository"; URL=$tmpDir1Path; Authentication = @{VaultName = "test"; Secret = ""}};  ErrorId = "InvalidAuthentication,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"}

    It "not register incorrectly formatted Name type repo among correct ones when incorrect type is <Type>" -TestCases $testCases2 {
        param($Type, $IncorrectHashTable, $ErrorId)

        $correctHashtable1 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly $ErrorId

        $res = Get-PSResourceRepository -Name "testRepository2"
        $res.Name | Should -Be "testRepository2"

        $res2 = Get-PSResourceRepository -Name "testRepository3"
        $res2.Name | Should -Be "testRepository3"

        $res3 = Get-PSResourceRepository -Name "PSGallery"
        $res3.Name | Should -Be "PSGallery"
        $res3.Priority | Should -Be 30
    }

    It "should register repository with relative location provided as URL" {
        Register-PSResourceRepository -Name "testRepository" -URL "./"
        $res = Get-PSResourceRepository -Name "testRepository"

        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }
}
