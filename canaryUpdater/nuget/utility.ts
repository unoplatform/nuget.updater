import * as tl from "azure-pipelines-task-lib/task";
import * as locationUtilities from "./locationUtilities";

export enum ProtocolType {
    NuGet,
    Maven,
    Npm,
    PyPi
}

export enum RegistryType {
    npm,
    NuGetV2,
    NuGetV3,
    PyPiSimple,
    PyPiUpload
}

interface RegistryLocation {
    apiVersion: string,
    area: string,
    locationId: string
};

export async function getNuGetFeedRegistryUrl(
    packagingCollectionUrl: string,
    feedId: string,
    accessToken?: string,
    useSession?: boolean): Promise<string>
{
    // If no version is received, V3 is assumed
    const registryType: RegistryType = RegistryType.NuGetV3;

    const overwritePackagingCollectionUrl = tl.getVariable("NuGet.OverwritePackagingCollectionUrl");
    if (overwritePackagingCollectionUrl) {
        tl.debug("Overwriting packaging collection URL");
        packagingCollectionUrl = overwritePackagingCollectionUrl;
    } else if (!packagingCollectionUrl) {
        const collectionUrl = tl.getVariable("System.TeamFoundationCollectionUri");
        packagingCollectionUrl = collectionUrl;
    }

    return await locationUtilities.getFeedRegistryUrl(packagingCollectionUrl, registryType, feedId, accessToken, useSession);
}