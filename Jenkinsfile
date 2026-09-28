@Library('CradleSharedLibrary@unity-buildprofiles') _ // Loaded implicitly

String Node = ''
String WorkingDir = ''
//Assign a node to run the pipeline
node('WindowsNode') {
    echo "Running on ${env.NODE_NAME} in ${env.WORKSPACE}"
    Node = env.NODE_NAME
    WorkingDir = env.WORKSPACE
}

// Config section

String gitHubRepo = "BredaUniversityResearch/Auggis"
String gitHubBranch = "${env.CHANGE_BRANCH}"
if (gitHubBranch == null || gitHubBranch == "" || gitHubBranch == "null") {
    gitHubBranch = "${env.BRANCH_NAME}"
}

String discordFriendlyName = "Auggis"

String nexusRepo = "MSP_ProceduralOceanViewUnity-Main"

String unityBuildName = "Auggis"
String unityVersion = "6000.6.0f1"

String outputBase = "Output"

// "constants", used for the map keys
String windowsServer = "WindowsServer"
String android = "Android"
String linuxServer = "LinuxServer"
def buildTargets = [windowsServer, android, linuxServer]

// maps
def buildNameMap = [:]
def buildNameDevMap = [:]
def outputFolderMap = [:]
def outputFolderDevMap = [:]
def paramNameMap = [:]
def descriptionMap = [:]
def unityBuildNameExtensionMap = [:]

// map values
buildNameMap[windowsServer] = "WindowsServer"
buildNameDevMap[windowsServer] = "WindowsServerDev"
outputFolderMap[windowsServer] = "CurrentWinBuild"
outputFolderDevMap[windowsServer] = "CurrentWinDevBuild"
paramNameMap[windowsServer] = "BUILD_WINDOWS_SERVER"
descriptionMap[windowsServer] = "Windows Server"
unityBuildNameExtensionMap[windowsServer] = '.exe'

buildNameMap[linuxServer] = "LinuxServer"
buildNameDevMap[linuxServer] = "LinuxServerDev"
outputFolderMap[linuxServer] = "CurrentUnityServerBuild"
outputFolderDevMap[linuxServer] = "CurrentUnityServerDevBuild"
paramNameMap[linuxServer] = "BUILD_LINUX_SERVER"
descriptionMap[linuxServer] = "Linux Server"
unityBuildNameExtensionMap[linuxServer] = ''

buildNameMap[android] = "AndroidClient"
buildNameDevMap[android] = "AndroidClientDev"
outputFolderMap[android] = "CurrentAndroidBuild"
outputFolderDevMap[android] = "CurrentAndroidDevBuild"
paramNameMap[android] = "BUILD_ANDROID_CLIENT"
descriptionMap[android] = "Android Client"
unityBuildNameExtensionMap[android] = '.apk'

properties([
    parameters([
        booleanParam(name: 'DEVELOPMENT', defaultValue: false, description: 'Development build?'),
        booleanParam(name: paramNameMap[windowsServer], defaultValue: false, description: "Make a ${descriptionMap[windowsServer]} build"),
        booleanParam(name: paramNameMap[android], defaultValue: true, description: "Make a ${descriptionMap[android]} build"),
        booleanParam(name: paramNameMap[linuxServer], defaultValue: true, description: "Make a ${descriptionMap[linuxServer]} build")
    ])
])

String discordWebhook = 'POV_DISCORD_WEBHOOK'

// End of Config section

Boolean cleanupBefore = false
Boolean cleanupAfter = true

String commit = ""
String messageIfStageFailure = ""
try { // we catch any exception that was unhandled
    stage('Clone') {
        node(Node) {
            try {
                if (cleanupBefore) {
                    dir(WorkingDir) {
                        deleteDir()
                    }
                    cleanWs()
                }
            } catch (Exception e) {
                messageIfStageFailure += "Failed to clean workspace: ${e.message}\n"
                catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                    error("Failed to clean workspace Exception: ${e.message}")
                }
                // Unrecoverable error, rethrowing to prevent next stages from executing
                throw e
            }
            try {
                git.checkoutWithSubModules("https://github.com/${gitHubRepo}", "${gitHubBranch}", 'CRADLE_WEBMASTER_CREDENTIALS')
                commit = git.fetchCommitHash('CRADLE_WEBMASTER_CREDENTIALS')
            } catch (Exception e) {
                messageIfStageFailure += "Failed to clone repositories: ${e.message}\n"
                catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                    error("Failed to clone repositories Exception: ${e.message}")
                }
                // Unrecoverable error, rethrowing to prevent next stages from executing
                throw e
            }
        }
    }
    stage('Build') {
        node(Node) {
            def context = createContext(buildNameMap, buildNameDevMap, outputFolderMap, outputFolderDevMap, descriptionMap)
            String env = params.DEVELOPMENT ? "Dev" : ""
            String buildNumber = "${currentBuild.number}"
            for (buildTarget in buildTargets) {
                def (outputFolder, buildName, tempMessageIfStageFailure) = getBuildDetails(buildTarget, params.DEVELOPMENT, buildNumber, commit, context)
                //messageIfStageFailure += tempMessageIfStageFailure + "\n"
                if (params[paramNameMap[buildTarget]]) {
                    stagesBuildAndUpload(buildTarget, outputFolder, buildName, env)
                } else {
                    stage(buildTarget+'Build') {
                        catchError(buildResult: 'SUCCESS', stageResult: 'NOT_BUILT') {
                            error(descriptionMap[buildTarget]+' Build was skipped')
                        }
                    }
                    stage("Zip${buildTarget}Build") {
                        catchError(buildResult: 'SUCCESS', stageResult: 'NOT_BUILT') {
                            error(descriptionMap[buildTarget]+' Zip was skipped')
                        }
                    }
                    stage("Upload${buildTarget}Build") {
                        catchError(buildResult: 'SUCCESS', stageResult: 'NOT_BUILT') {
                            error(descriptionMap[buildTarget]+' Upload was skipped')
                        }
                    }
                }
            }
        }
    }
} catch (InterruptedException e) {
    catchError(buildResult: 'ABORTED', stageResult: 'ABORTED') {
        error()
    }
    throw (e)
} catch (Exception e) {
    catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
        error()
    }
    throw (e)
} finally {
    stage('Report-Results') {
        node(Node) {
            try {
                switch (currentBuild.result) {
                    // discord unstable and failed use: webhook, name, reason
                    // discord succeeded uses: webhook, name, artifactlinks

                    case "UNSTABLE":
                        echo "Build was unstable"
                        discord.unstable(discordWebhook, discordFriendlyName, messageIfStageFailure)
                        break
                    case "FAILURE":
                        echo "Build failed"
                        discord.failed(discordWebhook, discordFriendlyName, messageIfStageFailure)
                        break
                    case "ABORTED":
                        echo "Build was aborted"
                        discord.failed(discordWebhook, discordFriendlyName, "Build was aborted")
                        break
                    default: // case "SUCCESS":
                        if (currentBuild.result != 'SUCCESS') {
                            echo "Unknown result, assuming build was successful"
                        }
                        def context = createContext(buildNameMap, buildNameDevMap, outputFolderMap, outputFolderDevMap, descriptionMap)
                        String links = ""
                        String buildNumber = "${currentBuild.number}"
                        for (buildTarget in buildTargets) {
                            if (params[paramNameMap[buildTarget]]) {
                                String buildName = getBuildName(buildTarget, params.DEVELOPMENT, buildNumber, commit, context)
                                String link = "https://nexus.cradle.buas.nl/#browse/browse:${nexusRepo}:${buildTarget}%%2F${buildName}"
                                links += "[Download ${descriptionMap[buildTarget]} Build from Nexus](${link});"
                            }
                        }
                        links = links.substring(0, links.length() - 1)
                        discord.succeeded(discordWebhook, discordFriendlyName, links)
                        break
                }
            // Catch any exceptions but we swallow them to ensure the cleanup happens
            } catch (InterruptedException e) {
                catchError(buildResult: 'ABORTED', stageResult: 'ABORTED') {
                    error()
                }
            } catch (Exception e) {
                catchError(buildResult: currentBuild.currentResult, stageResult: 'FAILURE') {
                    error("Unexpected failure during discord notification")
                }
            }
        }
    }
    stage('Cleanup') {
        if (cleanupAfter) {
            try {
                node(Node) {
                    dir(WorkingDir) {
                        deleteDir()
                    }
                    cleanWs()
                }
            } catch (Exception e) {
                echo "Unexpected failure during cleanup, retrying once..."
                try {
                    node(Node) {
                        dir(WorkingDir) {
                            deleteDir()
                        }
                        cleanWs()
                    }
                }
                catch (Exception ex) {
                    echo "Unexpected failure during cleanup retry: ${ex}"
                    throw (ex)
                }
            }
        }
    }
}

def createContext(buildNameMap, buildNameDevMap, outputFolderMap, outputFolderDevMap, descriptionMap)
{
    def context = [:]
    context['buildNameMap'] = buildNameMap
    context['buildNameDevMap'] = buildNameDevMap
    context['outputFolderMap'] = outputFolderMap
    context['outputFolderDevMap'] = outputFolderDevMap
    context['descriptionMap'] = descriptionMap
    return context
}

def getBuildDetails(buildTarget, useDev, buildNumber, commit, context)
{
    def outputFolder = getValue(buildTarget, context.outputFolderMap, context.outputFolderDevMap, useDev)
    def buildName = getBuildName(buildTarget, useDev, buildNumber, commit, context)
    def messageIfStageFailure = 'Failed to build '+context.descriptionMap[buildTarget]
    return [outputFolder, buildName, messageIfStageFailure]
}

def getBuildName(buildTarget, useDev, buildNumber, commit, context)
{
    return sanitizeinput.buildName(getValue(buildTarget, context.buildNameMap, context.buildNameDevMap, useDev), buildNumber, commit, "zip")
}

def getValue(buildTarget, values, devValues, useDev)
{
    return useDev ? devValues[buildTarget] : values[buildTarget]
}

def build(Node, WorkingDir, output, outputFolder, buildName, buildMethod, unityVersion, discordWebhook)
{
    build job: 'Library/WindowsUnityBuildV2',
    parameters: [
        string(name: 'WORKING_DIR', value: WorkingDir),
        string(name: 'NODE', value: Node),
        string(name: 'DISCORD_WEBHOOK', value: discordWebhook),
        string(name: 'UNITY_VERSION', value: "${unityVersion}"),
        string(name: 'PROJECT_PATH', value: "%CD%"),
        string(name: 'OUTPUT_PATH', value: "%CD%\\${output}\\${outputFolder}\\${buildName}"),
        string(name: 'BUILD_PROFILE_PATH', value: buildName)
    ]
}

def stagesBuildAndUpload(buildTarget, outputFolder, buildName, env)
{
    // we swallow any exceptions during the build, zip, and upload stages to ensure the pipeline continues for other build targets

    def prevStageSuccess = true
    stage(buildTarget+'Build') {
        try{
            build(
                Node, 
                WorkingDir, 
                outputBase, 
                outputFolder, 
                "${unityBuildName}${unityBuildNameExtensionMap[buildTarget]}", 
                "BuildUtility.${buildTarget}${env}Builder", 
                unityVersion, 
                discordWebhook)

        } catch (Exception e) {
            prevStageSuccess = false
            catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                error("Build failed for ${buildTarget} Exception: ${e.message}")
            }
        }
    }
    stage("Zip${buildTarget}Build") {
        try{
            if(prevStageSuccess){
                zip.pack(".\\${outputBase}\\${outputFolder}", buildName)
            }else{
                catchError(buildResult: 'FAILURE', stageResult: 'ABORTED') {
                    error("Previous stage failed for ${buildTarget}, skipping zip")
                }
            }
        } catch (Exception e) {
            prevStageSuccess = false
            catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                error("Zip failed for ${buildTarget} Exception: ${e.message}")
            }
        }
    }
    stage("Upload${buildTarget}Build") {
        try{
            if(prevStageSuccess){
                nexus.upload("${nexusRepo}", buildName, "application/x-zip-compressed", buildTarget, 'NEXUS_CREDENTIALS')
            }else{
                catchError(buildResult: 'FAILURE', stageResult: 'ABORTED') {
                    error("Previous stage failed for ${buildTarget}, skipping upload")
                }
            }
        } catch (Exception e) {
            prevStageSuccess = false
            catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                error("Upload failed for ${buildTarget}")
            }
        }
    }
}