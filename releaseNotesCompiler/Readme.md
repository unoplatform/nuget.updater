# nventive Release Notes compiler task

This straightforward task is used to help with the generation of basic release notes in the markdown format. The primary use case for those notes was AppCenter, but it can be adapted to fit any use. The flow is pretty simple, and consists mostly of gathering information on the pipeline run and writing those in a file. Here's a list of the information included in the resulting file:
- An optional environment passed as a parameter to the task (`EnvironmentName`) - useful to make sure that we're using the right build
- The name of the source branch, including a link to Azure DevOps
- The ID of the source commit, including a link to Azure DevOps
- The URL of the pipeline run, including a link to Azure DevOps
- Additional release notes taken from another file specified in the parameters (`AdditionalReleaseNotesFile`)

This task is able to produce a truncated version of the resulting release note file. This is very useful since some services (especially AppCenter) have a limit on the number of characters that can be included in the release notes. To do so, the `CreateTruncatedVersion`, `CharacterLimit` and `TruncatedOutputFilePath` parameters must be set.
It is also possible to remove the hyperlinks present in the release notes if they are not deemed necessary using the `RemoveHyperlinks` parameter.

The full-size file will be generated under the path specified in the `OutputFilePath` parameter. The task also provides an output variable called `ReleaseNotesPath` to use in subsequent steps.