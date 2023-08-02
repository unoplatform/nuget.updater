import * as path from "path";
import * as tl from "azure-pipelines-task-lib/task";

export interface LocateOptions {
    /** if true, search along the system path in addition to the hard-coded NuGet tool paths */
    fallbackToSystemPath?: boolean;

    /** Array of filenames to use when searching for the tool. Defaults to the tool name. */
    toolFilenames?: string[];

    /** Array of paths to search under. Defaults to agent NuGet locations */
    searchPath?: string[];

    /** root that searchPaths are relative to. Defaults to the Agent.HomeDirectory build variable */
    root?: string;
}

export function locateTool(tool: string, opts?: LocateOptions) {
    const defaultSearchPath = [""];
    const defaultAgentRoot = tl.getVariable("Agent.HomeDirectory");

    opts = opts || {};
    opts.toolFilenames = opts.toolFilenames || [tool];

    let searchPath = opts.searchPath || defaultSearchPath;
    let agentRoot = opts.root || defaultAgentRoot;

    tl.debug(`looking for tool ${tool}`);

    for (let thisVariant of opts.toolFilenames) {
        tl.debug(`looking for tool variant ${thisVariant}`);

        for (let possibleLocation of searchPath) {
            let fullPath = path.join(agentRoot, possibleLocation, thisVariant);
            tl.debug(`checking ${fullPath}`);
            if (tl.exist(fullPath)) {
                return fullPath;
            }
        }

        if (opts.fallbackToSystemPath) {
            tl.debug("Checking system path");
            let whichResult = tl.which(thisVariant);
            if (whichResult) {
                tl.debug(`found ${whichResult}`);
                return whichResult;
            }
        }

        tl.debug("not found");
    }

    return null;
}