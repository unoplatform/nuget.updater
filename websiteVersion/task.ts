import tl = require("azure-pipelines-task-lib");
import path = require("path");
import azure = require("azure-storage");

import armStorage = require('./azure-arm-rest/azure-arm-storage');
import { AzureRMEndpoint } from './azure-arm-rest/azure-arm-endpoint';
import { AzureEndpoint, StorageAccount } from './azure-arm-rest/azureModels';

import * as fs from 'fs';

class FileToUpload {
    localPath: string;
    name: string;
    remotePath: string;

    constructor(localPath: string, remotePrefix?: string, name?: string, ) {
        this.localPath = localPath;

        if (!name) {
            name = path.basename(localPath);
        }

        this.name = name;

        if (remotePrefix) {
            this.remotePath = remotePrefix + '/' + name;
        } else {
            this.remotePath = name;
        }
    }
}

const VersionFileName = 'Version.txt'
const ContainerName = '$web';
const IndexFileName = 'index.html';

let VersionsFolderName: string;
let BlobService: azure.BlobService;

async function run(): Promise<void> {
    try {
        BlobService = await createBlobService();

        let websitePath = tl.getInput("WebsitePath", true);
        VersionsFolderName = tl.getInput("VersionsFolderName", true);

        let currentVersion = await retrieveCurrentVersion(websitePath);

        await pushCurrentVersion(websitePath, currentVersion);

        await updateVersionsIndex();
        await updateRootIndex(currentVersion);
    }
    catch (ex) {
        tl.error(ex);
        tl.setResult(tl.TaskResult.Failed, ex.message);
    }
}

async function createBlobService() : Promise<azure.BlobService> {
    let subscription = tl.getInput('AzureSubscription', true);
    let storageAccountName = tl.getInput('AzureStorageAccount', true);

    var azureEndpoint: AzureEndpoint = await new AzureRMEndpoint(subscription).getEndpoint();
    const storageArmClient = new armStorage.StorageManagementClient(azureEndpoint.applicationTokenCredentials, azureEndpoint.subscriptionID);

    let storageAccount: StorageAccount = await storageArmClient.storageAccounts.get(storageAccountName);
    let storageAccountResourceGroupName = getResourceGroupNameFromUri(storageAccount.id);

    let accessKeys = await storageArmClient.storageAccounts.listKeys(storageAccountResourceGroupName, storageAccountName, null);
    let accessKey: string = accessKeys[0];

    return azure.createBlobService(storageAccountName, accessKey);
}

async function retrieveCurrentVersion(folderPath: string): Promise<string> {
    console.log("Retrieving website version");

    let fullVersionFilePath = path.join(folderPath, VersionFileName);
    if (fs.existsSync(fullVersionFilePath)) {
        let version = fs.readFileSync(fullVersionFilePath, "utf8").trim();

        if (version) {

            console.log("Version " + version + " found.");

            return version;
        }
    }

    throw "Could not determine version of the website. Ensure that " + VersionFileName + " exists and is correctly updated.";
}

async function listExistingVersions(): Promise<string[]> {

    console.log("Retrieving existing versions");

    var promise = new Promise<string[]>((resolve, reject) => {
        BlobService.listBlobDirectoriesSegmentedWithPrefix(ContainerName, VersionsFolderName + "/", null, function (error, result, response) {
            if (!!error) {
                reject(error);
            } else {
                let versions = new Array<string>();
                result.entries.forEach(directory => {
                    let version = directory.name.replace(VersionsFolderName, "").replace(/\//gi, "");

                    console.log("Found version " + version);

                    versions.push(version);
                });

                resolve(versions);
            }
        })
    });

    return promise;
}

async function pushCurrentVersion(localPath: string, version: string) {
    var files = listFiles(localPath, VersionsFolderName + "/" + version);

    for (var i in files) {
        let file = files[i];
        await uploadFile(file);

        if(file.name.endsWith('.wasm')) {
            await updateFile(file, { contentType: "application/wasm" });
        }
    }
}

async function updateVersionsIndex() {

    let availableVersions = await listExistingVersions();

    let html = '<html>';
    html += '\n' + '<head>';
    html += '\n' + '<title>Available version</title>';
    html += '\n' + '<style>';
    html += '\n' + 'h1{font:400 40px/1.5 Helvetica,Verdana,sans-serif;margin:0;padding:0}ul{list-style-type:none;margin:0;padding:0}li{font:200 20px/1.5 Helvetica,Verdana,sans-serif;border-bottom:1px solid #ccc}li:last-child{border:none}li a{text-decoration:none;display:block;width:200px;-webkit-transition:font-size .3s ease,background-color .3s ease;-moz-transition:font-size .3s ease,background-color .3s ease;-o-transition:font-size .3s ease,background-color .3s ease;-ms-transition:font-size .3s ease,background-color .3s ease;transition:font-size .3s ease,background-color .3s ease}li a:hover{font-size:30px}@media (prefers-color-scheme:light){li a{color:#f6f6f6}li a:hover{background:#000}}@media (prefers-color-scheme:dark){li a{color:#000}li a:hover{background:#f6f6f6}}';
    html += '\n' + '</style>';
    html += '\n' + '</head>';
    html += '\n' + '<body><div>';
    html += '\n' + '<h1>Available Versions for ' + tl.getVariable("Build.DefinitionName") + '</h1>';
    html += '\n' + '<ul>';

    availableVersions
        .sort((a, b) => -compareVersions(a, b))
        .forEach(v => {
            html += '\n' + '<li><a href="/' + VersionsFolderName + '/' + v + '/">' + v + '</a></li>';
        });

    html += '\n' + '</ul>';
    html += '\n' + '</div></body>';
    html += '\n' + '</html>';

    console.log("Generated " + VersionsFolderName + IndexFileName);
    console.log(html);

    let indexPath = path.join(__dirname, IndexFileName);
    fs.writeFileSync(indexPath, html);

    await uploadFile(new FileToUpload(indexPath, VersionsFolderName));
}

async function updateRootIndex(currentVersion: string) {
    let html = '<html>';
    html += '\n' + '<head>';
    html += '\n' + '<meta http-equiv="refresh" content="0; URL=\'/' + VersionsFolderName + '/' + currentVersion + '/\'" />';
    html += '\n' + '</head>';
    html += '\n' + '</html>';

    console.log("Generated " + IndexFileName);
    console.log(html);

    let indexPath = path.join(__dirname, IndexFileName);
    fs.writeFileSync(indexPath, html);

    await uploadFile(new FileToUpload(indexPath));
}

async function uploadFile(file: FileToUpload) {

    let upload = new Promise((resolve, reject) => {
        BlobService.createBlockBlobFromLocalFile(ContainerName, file.remotePath, file.localPath, function (error, result, response) {
            if (!error) {
                console.log("Pushed " + result.name);
                resolve();
            } else {
                reject(error);
            }
        });
    });

    await upload;
}

async function updateFile(file: FileToUpload, options: azure.BlobService.SetBlobPropertiesRequestOptions) {

    let upload = new Promise((resolve, reject) => {
        BlobService.setBlobProperties(ContainerName, file.remotePath, options, function (error, result, response) {
            if (!error) {
                console.log("Updated " + result.name);
                resolve();
            } else {
                reject(error);
            }
        });
    });

    await upload;
}
function listFiles(folderPath: string, prefix: string): FileToUpload[] {
    let files = new Array<FileToUpload>();

    fs.readdirSync(folderPath).forEach(item => {
        let itemPath = path.join(folderPath, item);
        if (fs.statSync(itemPath).isDirectory()) {
            listFiles(itemPath, prefix + "/" + path.basename(item)).forEach(i => files.push(i));
        } else {
            files.push(new FileToUpload(itemPath, prefix));
        }
    });

    return files;
}

function compareVersions(versionA: string, versionB: string): number {
    let partsA = versionA.split(".");
    let partsB = versionB.split(".");

    let partsSize = Math.max(partsA.length, partsB.length);

    for (var i = 0; i < partsSize; i++) {
        let a = tryGetNumber(partsA, i, 0);
        let b = tryGetNumber(partsB, i, 0);

        if (a == b) {
            continue;
        }

        return a - b;
    }

    return 0;
}

function tryGetNumber(values: string[], index: number, defaultValue: number): number {
    if (index >= values.length) {
        return defaultValue;
    } else {
        return +values[index];
    }
}


function getResourceGroupNameFromUri(resourceUri: string): string {
    if (!!resourceUri && !!resourceUri.trim()) {
        resourceUri = resourceUri.toLowerCase();
        return resourceUri.substring(resourceUri.indexOf("resourcegroups/") + "resourcegroups/".length, resourceUri.indexOf("/providers"));
    }

    return "";
}
run();