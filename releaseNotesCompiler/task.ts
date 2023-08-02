import tl = require("azure-pipelines-task-lib");
import fs = require('fs');

async function run() {
    let output = addLine("", "# Details");

    output = addLine(output, getEnvironmentContent());

    output = addLine(output, getSourceBranchContent());

    output = addLine(output, getSourceCommitContent());

    output = addLine(output, getPipelineUrl());

    output = addLine(output, getAdditionalReleaseNotesContent());

    let outputFile = tl.getInput("OutputFilePath");
    
    console.log("Generating release notes in " + outputFile);
    
    await writeOutputToFile(output, outputFile);

    tl.setVariable("ReleaseNotesPath", outputFile);

    if(tl.getBoolInput("CreateTruncatedVersion")) {
        let characterLimit = parseInt(tl.getInput("CharacterLimit"));
        let truncatedFilePath = tl.getInput("TruncatedOutputFilePath");

        console.log("Generating release notes truncated at " + characterLimit + " characters in " + truncatedFilePath);

        await writeOutputToFile(trimOutput(output, characterLimit, "..."), truncatedFilePath);

        tl.setVariable("TruncatedReleaseNotesPath", truncatedFilePath);
    }
}

function getEnvironmentContent() : string {
    let environment = tl.getInput("EnvironmentName");

    if(environment) {
        return "- Environment: " + environment;
    }

    return null;
}

function getSourceBranchContent() : string {
    let branchName = tl.getVariable("Build.SourceBranch").replace("refs/heads/","");
    let repositoryUrl = GetRepositoryUrl();
    let branchUrl: string;
    
    if(repositoryUrl) {
        branchUrl = repositoryUrl + "?version=GB" + encodeURIComponent(branchName);
    }

    return "- Source branch: " + getHyperlinkMarkdown(branchName, branchUrl);
}

function getSourceCommitContent() : string {
    let sourceVersion = tl.getVariable("Build.SourceVersion");
    let repositoryUrl = GetRepositoryUrl();
    let commitUrl: string;

    if(repositoryUrl) {
        commitUrl = repositoryUrl + "/commit/" + sourceVersion + "?refName=" + encodeURIComponent(tl.getVariable("Build.SourceBranch"));
    }

    return "- Source commit: " + getHyperlinkMarkdown(sourceVersion, commitUrl);
}

function getPipelineUrl() : string {
    let buildNumber = tl.getVariable("Build.BuildNumber");
    let buildId = tl.getVariable("Build.BuildId");
    let projectUrl = GetProjectUrl();
    let pipelineUrl: string;

    if(projectUrl && buildNumber) {
        pipelineUrl = projectUrl + "/_build/results?buildId=" + buildId + "&view=results";
    }

    return "- Pipeline run : " + getHyperlinkMarkdown(buildNumber, pipelineUrl);
}

function getAdditionalReleaseNotesContent() : string {
    let additionalNotes = tl.getInput("AdditionalReleaseNotesFile");
    if(additionalNotes && fs.existsSync(additionalNotes)) {
        return fs.readFileSync(additionalNotes).toString();
    }

    return null;
}

function GetRepositoryUrl() : string {
    let repositoryUrl = tl.getVariable("Build.Repository.Uri");
    if(repositoryUrl) {
        repositoryUrl = repositoryUrl.replace(/https:\/\/.*@/gi, "https://"); //Remove user@ in the repo URL
    }

    return repositoryUrl;
}

function GetProjectUrl() : string {
    let collectionUrl = tl.getVariable("System.TeamFoundationCollectionUri");
    let project = tl.getVariable("System.TeamProject");

    return collectionUrl + project.replace(" ", "%20");
}

function getHyperlinkMarkdown(text: string, url: string) : string {
    if(url) {
        return "[" + text + "](" + url + ")";
    }
    else {
        return text;
    }
}

function trimOutput(output: string, limit: number, trimCharacters: string) : string {
    if(tl.getBoolInput("RemoveHyperlinks")) {
        output = output.replace(/\[(.*?)\]\(.*?\)/g, '$1');
    }

    let characterCount = 0;
    let trimmedOutput : string[] = [];
    let limitWithCharacter = limit - trimCharacters.length;
    
    for(var line of output.split('\n')) {
        var newCharacterCount = characterCount + line.length + 1; //Adding 1 for the newline character we will add at the end
         //new line doesn't fit
        if(newCharacterCount > limit) {
            tl.debug("Cannot fit current line; character count is " + characterCount);
            var lineLimit = limitWithCharacter - characterCount;
            //Check if we have enough space to put the lines + the trim characters
            if(lineLimit >= 0) {
                tl.debug("Trimming current line to " + lineLimit + " characters");
                trimmedOutput.push(line.substring(0, lineLimit) + trimCharacters);
            }
            //otherwise, we add the trim characters to the previous eligible line
            else if(trimmedOutput.length > 0) {
                var previousLine = trimmedOutput.pop();
                var previousLineLimit = limitWithCharacter - characterCount - previousLine.length;

                tl.debug("Trimming previous line to " + previousLineLimit + " characters");
                
                trimmedOutput.push(previousLine.substring(0, previousLineLimit) + trimCharacters);
            }
            break;
        }

        characterCount = newCharacterCount;

        trimmedOutput.push(line);
    }
    return trimmedOutput.join('\n');
}

function addLine(input: string, line: string) : string {
    if(line) {
        return input + line + '\n';
    }
    else {
        return input;
    }
}

async function writeOutputToFile(output: string, outputFilePath: string): Promise<boolean> {
    if(!outputFilePath) {
        return;
    }

    try {
        if(fs.existsSync(outputFilePath)) {
            const fileStates = fs.statSync(outputFilePath);
            var file = fs.createWriteStream(outputFilePath, { flags: "r+", autoClose: true, start: fileStates.size });

            file.write(output);
        } else {
            var file = fs.createWriteStream(outputFilePath, { autoClose: true });

            file.write(output);
        }

        return true;
    } catch(ex)
    {
        tl.error(ex);
        return false;
    }
}

run();