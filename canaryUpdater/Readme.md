# nventive Canary Updater task

This task is meant to be used by nventive Canary process. The flow of this task is as follows

## 1. Merge
If requested, the task merges the current branch with the one indicated in the parameters. To do so, and if the branch is not in the current repository, a remote is added to the Git repository and the specfied branch is fetched. 
Once the branch is fetched, and once again if requested, it is pushed to the current repository. This is used to keep an updated version of the target branch in the current repository.
Finally, a merge is run between the two branches, taking the target branch changes in priority. This is achieved using the `-X theirs` parameters of the merge command. This means that changes from the current branches might be overriden by the target branch, but it helps with the maintenance of the canaries.

## 2. NuGet update
This is the core of the Canary task. This step updates the NuGet packages present in the branch, using [NuGet Updater](https://github.com/nventive/NuGet.Updater/tree/develop/src/NvGet.Tools.Updater#readme). The majority of the NuGet Updater parameters are mapped directly in the task, and the full command is printed in the output, making it easy to debug directly.

The target versions parameter can be either explicit or calculated from the source branch. If it is not specified, the target version will be set to whatever is after `canaries/` in the branch name. Multiple versions can be specified using the `+` sign. If the branch doesn't have this format, the task will fail. `stable` will always be included if the target version is calculated this way.

## 3. Branch push
Once again, this step is optional. It pushes a new branch with all the changes introduced with both the merge and the NuGet update. The new branch will have a name generated from the target version specified in either the parameters or in the branch name directly, the build number and be prefixed with `canaries/build`. It is advised to configure the pipeline to have the following build number pattern: `$(Date:yyyyMMdd)$(Rev:.r)`.

For example, in the scenario where this task would be run on a branch named `canaries/dev`, the pushed branch would be named `canaries/build/dev/20211004.1`