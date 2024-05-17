This extensions gives access the following build tasks :

# Canary Updater
A task allowing to automatically update NuGet packages to the latest version.
The canary process is meant to be run in its own branch and is a two step process:
- Merge a working branch in the canary branch
- Use [NuGet.Updater](https://github.com/unoplatform/NuGet.Updater/blob/develop/src/NvGet.Tools.Updater/Readme.md) to update the packages to the latest matching version
