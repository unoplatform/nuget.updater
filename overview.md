This extensions gives acess the following build tasks :

# Canary Updater
A task allowing to automatically update NuGet packages to the latest version.
The canary process is meant to be run in its own branch and is a two step process:
- Merge a working branch in the canary branch
- Use [NuGet.Updater](https://github.com/nventive/NuGet.Updater/blob/develop/src/NvGet.Tools.Updater/Readme.md) to update the packages to the latest matching version

# Release Notes Compiler
A task generating a simple set of release notes in the markdown format. By default, these notes contain the following information:
- The name and a link to the branch from which the build was run
- The commit id and a link to it
- A link the pipeline run where this task was executed
- Optionally, the name of an environment provided to the task
- Another release note file can also be appended to the notes generated.

A truncated version of the release notes can be generated to accomodate limitations from certain services.
# Website versioning
A task providing a custom solution for website versioning. It works as follows:
- At the location where the website is stored, a "versions" folder is created, in which a version-specific folder is created for the current version of the website being deployed
- The current version of the website is uploaded in the version folder
- The index page at the root of website is configured to redirect to the version folder
- Another index file is present in the versions folder, allowing access to all previous versions

This task currently only supports a website hosted in an Azure Storage account.