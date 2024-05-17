# unoplatform Website versioning

This task is meant to help with versioning a static website. It was developed as a mean to maintain multiple versions of WASM-based Uno applications. The concept is very simple:
- The contents of the website are stored in an Azure Blob storage
- All the versions of the website can be found under a `Versions` folder
- The list of the available versions can be found by navigating to /Versions
- When navigating to the root of the website, the user is immediately redirected to the latest version, using `http-equiv="refresh"`

This task is in charge of uploading the files to the storage account and generating the 2 HTML files - the root index and the versions index.

The website should be somehow versioned for this to work properly, using a file named Version.txt placed at the root of the website.