import tl = require("azure-pipelines-task-lib");
import fs = require('fs');
import * as nutil from "./nuget/utility";
import * as pkgLocationUtils from "./nuget/locationUtilities";
import { IExecOptions } from "azure-pipelines-task-lib/toolrunner";
import { Writable } from "stream";
import path = require("path");

const canaryUpdateVariableName: string = "IsCanaryUpdated";
const canaryBranchPrefix: string = "refs/heads/canaries/";
const mergedBranchFolder: string = "build";

let targetVersions : string[] = [];
let pushedBranchFolder: string = "";
let mergeOutput = "\n# Merge summary\n```\n";

let isGitUserSet = false;

async function run() {
    console.log("Updating Canary build");

    let isMergeSuccessful = await gitMerge();

    if (!isMergeSuccessful) {
        tl.setResult(tl.TaskResult.Failed, "Failed to complete the merge");
        return;
    }

    let isNugetUpdateSuccessful = await nugetUpdate();

    if (!isNugetUpdateSuccessful) {
        tl.setResult(tl.TaskResult.Failed, "Failed to update nuget packages");
        return;
    }

    let isWriteSummarySuccessful = await writeMergeSummary();

    if(!isWriteSummarySuccessful) {
        tl.setResult(tl.TaskResult.Failed, "Failed to write canary update summary to " + tl.getInput("summaryFile"));
        return;
    }

    let isBranchPushSuccessful = await pushBranch();

    if (!isBranchPushSuccessful) {
        tl.setResult(tl.TaskResult.Failed, "Failed to push new branch. Make sure that the Build Service has the proper rights on the target repository.");
        return;
    }

    tl.setVariable(canaryUpdateVariableName, "True");
    tl.setResult(tl.TaskResult.Succeeded, "Canary Updated");
}

async function setGitUser(): Promise<boolean> {

    if(isGitUserSet) {
        return true;
    }
    let email = tl.getInput("gitUserEmail");
    let name = tl.getInput("gitUserName");

    let gitTool = tl.tool(tl.which("git"))
        .arg([ "config", "user.email", email ]);

    await gitTool.exec();

    gitTool = tl.tool(tl.which("git"))
        .arg(["config", "user.name", name]);

    await gitTool.exec();

    isGitUserSet = true;

    return true;
}

async function gitMerge(): Promise<boolean> {
    try {
        if(!tl.getBoolInput("mergeBranch")) {
            return true;
        }

        await setGitUser();

        let remote = "origin";

        let isMergeBranchAway = tl.getBoolInput("isBranchToMergeAway");
        let branch = tl.getInput("branchToMerge");

        if(isMergeBranchAway) {
            remote = "merge";
            try {
                await tl.tool(tl.which("git"))
                    .arg([ "remote", "remove", remote])
                    .exec();
            } catch (error) {
                //remote doesn't exist
            }

            await tl.tool(tl.which("git"))
                .arg([ "remote", "add", remote, getMergeRepositoryUrl()])
                .exec();

            await tl.tool(tl.which("git"))
                .arg(["fetch", "--prune", "--progress", remote, branch ])
                .exec();
        } else if(tl.getVariable("Build.Repository.Provider") == "TfsGit") { //Azure DevOps repository
            let accessToken = tl.getVariable("System.AccessToken");
            
            await tl.tool(tl.which("git"))
                .line('-c http.extraheader="AUTHORIZATION: bearer ' + accessToken + '"')
                .arg(["fetch", "--prune", "--progress", remote, branch ])
                .exec();    
        } else {            
            await tl.tool(tl.which("git"))
                .arg(["fetch", "--prune", "--progress", remote, branch ])
                .exec();    
        }

        if(isMergeBranchAway && tl.getBoolInput("pushMergeBranch")) {

            console.log("Pushing " + branch + " to origin");

            await tl.tool(tl.which("git"))
                .arg(["push", "--force", "origin", remote + "/" + branch + ":refs/heads/" + branch])
                .exec();
        }

        console.log("Merging with " + branch);

        let mergeTool = tl.tool(tl.which("git"))
            .arg([ "merge", remote + "/" + branch, "-s", "recursive", "-X", "theirs" ]);

        let returnCode = await mergeTool.exec(<IExecOptions>{ outStream: getConsoleStream(true) });

        mergeOutput += "```\n";

        return returnCode == 0;
    }
    catch (ex) {
        tl.error(ex);

        return false;
    }
}

function getMergeRepositoryUrl(): string {
    let connection = tl.getInput("mergeRepositoryConnection", true);
    let url = encodeURI(tl.getEndpointUrl(connection, false));
    let token = tl.getEndpointAuthorizationParameter(connection, "password", false);
    //Including the token in the remote url
    return url.replace(/https:\/\/.*@/g, "https://" + token + "@");
}

async function nugetUpdate(): Promise<boolean> {
    let solution = tl.getInput("solution");
    let nugetFeed = await getFeedUrl();
    let packageAuthor = tl.getInput("packageAuthor");
    let allowDowngrade = tl.getBoolInput("allowDowngrade");
    let useNuGetOrg = tl.getBoolInput("useNuGetOrg");
    let strict = tl.getBoolInput("strict");
    let useVersionOverrides = tl.getBoolInput("useVersionOverrides");
    let versionOverridesFile = tl.getInput("versionOverridesFile");
    let useUpdateProperties = tl.getBoolInput("useUpdateProperties");
    let updatePropertiesFile = tl.getInput("updatePropertiesFile");
    let summaryFile = tl.getInput("summaryFile");
    let resultFile = tl.getInput("resultFile");

    targetVersions = await getTargetVersions();

    if (targetVersions.length > 0) {
        console.log("Target versions: [" + targetVersions.join(",") + "]");
    }

    try {
        await installNuGetUpdater();

        console.log("Executing Nuget.Updater task");

        let summaryFileArg = null;
        let versionOverridesFileArg = null;
        let updatePropertiesFileArg = null;
        let resultFileArg = null;
        let feedArg = null;
        let downgradeArg = null;
        let useNuGetOrgArg = null;
        let strictArg = null;

        if(summaryFile) {
            summaryFileArg = "--outputFile=" + summaryFile;
        }

        if(useVersionOverrides && versionOverridesFile) {
            versionOverridesFileArg = "--versionOverrides=" + versionOverridesFile;
        }

        if (useUpdateProperties && updatePropertiesFile) {
            updatePropertiesFileArg = "--updateProperties=" + updatePropertiesFile;
        }

        if (resultFile) {
            resultFileArg = "--result=" + resultFile;
        }

        if(nugetFeed) {
            feedArg = "--feed=" + nugetFeed + "|" + tl.getVariable("System.AccessToken");
        }

        if(allowDowngrade) {
            downgradeArg = "--allowDowngrade";
        }

        if(useNuGetOrg) {
            useNuGetOrgArg = "--useNuGetorg";
        }

        if(strict) {
            strictArg = "--strict";
        }

        let updateTool = tl
            .tool(path.join(tl.getVariable("Agent.TempDirectory"), "nugetupdater"))
            .arg([
                "--solution=" + solution,
                feedArg,
                useNuGetOrgArg,
                "--packageAuthor=" + packageAuthor,
                downgradeArg,
                summaryFileArg,
                resultFileArg,
                strictArg,
                versionOverridesFileArg,
                updatePropertiesFileArg
            ]
            .concat(targetVersions.map(v => "--version=" + v))
            .concat(getListArgument("ignorePackages", ";", "ignore"))
            .concat(getListArgument("updatePackages", ";", "update"))
            .concat(getListArgument("additionalPublicSources", "\n", "feed"))
            .filter(isNotNull)
            );

        await updateTool.exec(<IExecOptions>{ outStream: getConsoleStream(false) });

        return true;
    }
    catch (ex) {
        tl.error(ex);
        return false;
    }
}

function isNotNull(element, index, array) { 
   return element != null; 
} 

async function getTargetVersions(): Promise<string[]> {
    var inputTargetVersion = tl.getDelimitedInput("nugetVersion", ",");

    if(!inputTargetVersion || inputTargetVersion.length == 0) {
        try
        {
            let branch = tl.getVariable("Build.SourceBranch");
        
            if(branch.startsWith(canaryBranchPrefix)) {
                let branchParts = branch.replace(canaryBranchPrefix, "").split("/");
                let branchName = branchParts.pop();

                //if there's a folder before the actual branch name, we keep it aside to push under the same path
                pushedBranchFolder = branchParts.join("/");

                let targetVersions = [];

                if(branchName.includes("+")) {
                    targetVersions = branchName.split("+");
                } else {
                    targetVersions = [ branchName ];
                }

                if(!targetVersions.includes("stable")) {
                    targetVersions.push("stable");
                }
                
                return targetVersions;
            }
        
            throw "Invalid source branch. This task must be used on a branch named canaries/{target package version}";
        }
        catch(ex)
        {
            tl.error(ex);
            return [];
        }
    }

    return inputTargetVersion;
}

function getListArgument(inputName: string, inputDelimiter: string, argumentName: string): string[] {
    var input = tl.getDelimitedInput(inputName, inputDelimiter);

    if(input && input.length > 0) {
        return input.map(i => "--" + argumentName + "=" + i);
    }

    return [];
}

async function writeMergeSummary(): Promise<boolean> {

    let summaryFile = tl.getInput("summaryFile");

    if(!summaryFile || summaryFile == "") {
        return true;
    }

    try {
        if(fs.existsSync(summaryFile)) {
            const summaryFileStats = fs.statSync(summaryFile);
            var file = fs.createWriteStream(summaryFile, { flags: "r+", autoClose: true, start: summaryFileStats.size });

            file.write(mergeOutput);
        } else {
            var file = fs.createWriteStream(summaryFile, { autoClose: true });

            file.write(mergeOutput);
        }

        return true;
    } catch(ex)
    {
        tl.error(ex);
        return false;
    }
}

async function pushBranch(): Promise<boolean> {    
    try {
        if(!tl.getBoolInput("pushBranch")) {
            return true;
        }

        await setGitUser();

        //for a dev canary branch name will be "canaries/build/dev/build_XXXXXXXXXX.X"
        let branchName = "canaries/" + mergedBranchFolder + "/";

        if(pushedBranchFolder != "") {
            branchName +=  pushedBranchFolder + "/";
        }

        branchName += targetVersions[0] + "/" + mergedBranchFolder + "_" + tl.getVariable("Build.BuildNumber");

        let summaryFile = tl.getInput("summaryFile");
        if(summaryFile) {
            await tl.tool(tl.which("git"))
                .arg([ "add", summaryFile ])
                .exec();
        }

        let resultFile = tl.getInput("resultFile");
        if(resultFile) {
            await tl.tool(tl.which("git"))
                .arg([ "add", resultFile ])
                .exec();
        }

        let commitMessage = "Updated packages to " + targetVersions.join(",");

        if(tl.getBoolInput("mergeBranch")){
            commitMessage = commitMessage + " and merged " + tl.getInput("branchToMerge");
        }

        let commitTool = tl.tool(tl.which("git"))
            .arg([ "commit", "-am", commitMessage ]);

        await commitTool.exec();

        let pushTool = tl.tool(tl.which("git"))
            .arg([ "push", "origin", "HEAD:refs/heads/" + branchName ]);

        await pushTool.exec();

        return true;
    }
    catch (ex) {
        tl.error(ex);

        return false;
    }
}

async function installNuGetUpdater(): Promise<boolean> {
    let toolVersion = tl.getInput("nugetUpdaterVersion");
    let dotnetPath = tl.which("dotnet");
    let dotnet = tl.tool(dotnetPath);

    let installationTool = dotnet
        .arg([ "tool", "install", "unoplatform.NuGet.Updater.Tool", "--version", toolVersion, "--tool-path", tl.getVariable("Agent.TempDirectory"), "--ignore-failed-sources" ]);

    await installationTool.exec(<IExecOptions>{ outStream: getConsoleStream(false) });

    return true;
}

function getConsoleStream(writeToMergeOutput: boolean) : Writable {
    
    var stream = new Writable();

    stream._write = (chunk, e, next) => {
        let line = chunk.toString();
        console.log(line);
        if(writeToMergeOutput) {
            mergeOutput += line + "\n";
        }
        next();
    };

    return stream;
}

async function getFeedUrl(): Promise<string> {
    var feed = tl.getInput("nugetFeed");

    if(!feed) {
        return null;
    }

    try {
        let packagingLocation = await pkgLocationUtils.getPackagingUris(pkgLocationUtils.ProtocolType.NuGet);
        return await nutil.getNuGetFeedRegistryUrl(packagingLocation.DefaultPackagingUri, feed, pkgLocationUtils.getSystemAccessToken());
    }
    catch (error) {
        tl.setResult(tl.TaskResult.Failed, error.message);
        return null;
    }
}

run();