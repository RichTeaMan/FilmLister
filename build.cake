#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#tool nuget:?package=vswhere
#addin "Cake.Incubator"
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var tmdbApiKey = Argument("tmdbApiKey", string.Empty);

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory(configuration);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories("./**/bin/**");
});

Task("Restore-NuGet-Packages")
    .Does(() =>
{
    DotNetCoreRestore("./FilmLister.sln");
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    DotNetCoreBuild("./FilmLister.sln", new DotNetCoreBuildSettings {
    Verbosity = DotNetCoreVerbosity.Minimal,
    Configuration = configuration
    });

    var publishSettings = new DotNetCorePublishSettings
    {
        Configuration = configuration
    };
    DotNetCorePublish("./FilmLister.WebUI/FilmLister.WebUI.csproj", publishSettings);
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest("FilmLister.Server.Tests/FilmLister.Server.Tests.csproj");
    DotNetCoreTest("FilmLister.Domain.Test/FilmLister.Domain.Test.csproj");
});

Task("WebUI")
    .IsDependentOn("Build")
    .Does(() =>
{
    var publishDirectory = $"./FilmLister.WebUI/bin/{buildDir}/netcoreapp2.2/publish";
    var executeSettings = new DotNetCoreExecuteSettings
    {
        WorkingDirectory = publishDirectory
    };

    DotNetCoreExecute($"{publishDirectory}/FilmLister.WebUI.dll", $"--TmdbApiKey {tmdbApiKey}", executeSettings);
});

Task("WebUIDocker")
    .IsDependentOn("Build")
    .Does(() =>
{
    var publishDirectory = $"./FilmLister.WebUI/bin/{buildDir}/netcoreapp2.2/publish";
    var executeSettings = new DotNetCoreExecuteSettings
    {
        WorkingDirectory = publishDirectory
    };
    CopyFile(
        "./FilmLister.WebUI/appsettings.Docker.json",
        $"{publishDirectory}/appsettings.json");
    
    DotNetCoreExecute($"{publishDirectory}/FilmLister.WebUI.dll", $"--TmdbApiKey {tmdbApiKey}", executeSettings);
});

Task("UpdateDatabase")
    .IsDependentOn("Build")
    .Does(() =>
{
    using(var process = StartAndReturnProcess("dotnet", new ProcessSettings {
        Arguments = "ef database update --project FilmLister.Persistence --startup-project FilmLister.WebUI"}))
    {
        process.WaitForExit();
        int exitCode = process.GetExitCode();
        if (exitCode != 0) {
            throw new Exception("Failed to update database.");
        }
    }

    var publishDirectory = $"./FilmLister.Service.Updater/bin/{buildDir}/netcoreapp2.2";
    DotNetCoreExecute($"{publishDirectory}/FilmLister.Service.Updater.dll");
});

Task("UpdateDatabaseDocker")
    .IsDependentOn("Build")
    .Does(() =>
{
    var publishDirectory = $"./FilmLister.Service.Updater/bin/{buildDir}/netcoreapp2.2";
    var webUIPublishDirectory = $"./FilmLister.WebUI/bin/Debug/netcoreapp2.2";
    CopyFile(
        "./FilmLister.Service.Updater/appsettings.Docker.json",
        $"{publishDirectory}/appsettings.json");

    CopyFile(
        "./FilmLister.WebUI/appsettings.Docker.json",
        $"{webUIPublishDirectory}/appsettings.json");

    using(var process = StartAndReturnProcess("dotnet", new ProcessSettings {
        Arguments = "ef database update --project FilmLister.Persistence --startup-project FilmLister.WebUI -v"}))
    {
        process.WaitForExit();
        int exitCode = process.GetExitCode();
        if (exitCode != 0) {
            throw new Exception("Failed to update database.");
        }
    }

    DotNetCoreExecute($"{publishDirectory}/FilmLister.Service.Updater.dll");
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Test");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
