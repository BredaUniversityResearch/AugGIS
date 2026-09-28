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
if (gitHubBranch == null || gitHubBranch == "" || gitHubBranch == "null") {
    // 'Pipeline script from SCM' jobs don't inject GIT_BRANCH/BRANCH_NAME into env, but they do expose
    // the job's own SCM configuration via the 'scm' global, so read the configured branch specifier from there.
    try {
        gitHubBranch = scm.branches[0].name.replaceFirst(/^\*\//, "").replaceFirst(/^origin\//, "")
    } catch (Exception ignored) {
        gitHubBranch = null
    }
}
if (gitHubBranch == null || gitHubBranch == "" || gitHubBranch == "null") {
    error("Could not determine which branch to build: env.CHANGE_BRANCH, env.BRANCH_NAME are unset, and no 'scm' branch specifier is available. Run this Jenkinsfile from a Multibranch Pipeline job, or a job configured with 'Pipeline script from SCM'.")
}

String discordFriendlyName = "Auggis"

String nexusRepo = "MSP_ProceduralOceanViewUnity-test"

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
def paramNameMap = [:]
def descriptionMap = [:]
def unityBuildNameExtensionMap = [:]

// map values
buildNameMap[windowsServer] = "WindowsServer"
buildNameDevMap[windowsServer] = "WindowsServerDev"
outputFolderMap[windowsServer] = "CurrentWinBuild"
paramNameMap[windowsServer] = "BUILD_WINDOWS_SERVER"
descriptionMap[windowsServer] = "Windows Server"
unityBuildNameExtensionMap[windowsServer] = '.exe'

buildNameMap[linuxServer] = "LinuxServer"
buildNameDevMap[linuxServer] = "LinuxServerDev"
outputFolderMap[linuxServer] = "CurrentUnityServerBuild"
paramNameMap[linuxServer] = "BUILD_LINUX_SERVER"
descriptionMap[linuxServer] = "Linux Server"
unityBuildNameExtensionMap[linuxServer] = ''

buildNameMap[android] = "AndroidClient"
buildNameDevMap[android] = "AndroidClientDev"
outputFolderMap[android] = "CurrentAndroidBuild"
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
            def buildConfig = createBuildConfig(buildNameMap, buildNameDevMap, outputFolderMap, descriptionMap, unityBuildNameExtensionMap)
            String env = params.DEVELOPMENT ? "Dev" : ""
            String buildNumber = "${currentBuild.number}"
            for (buildTarget in buildTargets) {
                def platform = getPlatformContext(buildTarget, params.DEVELOPMENT, buildNumber, commit, buildConfig)
                if (params[paramNameMap[buildTarget]]) {
                    stagesBuildAndUpload(platform, env)
                } else {
                    stage(platform.target+'Build') {
                        catchError(buildResult: 'SUCCESS', stageResult: 'NOT_BUILT') {
                            error(platform.description+' Build was skipped')
                        }
                    }
                    stage("Zip${platform.target}Build") {
                        catchError(buildResult: 'SUCCESS', stageResult: 'NOT_BUILT') {
                            error(platform.description+' Zip was skipped')
                        }
                    }
                    stage("Upload${platform.target}Build") {
                        catchError(buildResult: 'SUCCESS', stageResult: 'NOT_BUILT') {
                            error(platform.description+' Upload was skipped')
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
                        def buildConfig = createBuildConfig(buildNameMap, buildNameDevMap, outputFolderMap, descriptionMap, unityBuildNameExtensionMap)
                        String links = ""
                        String buildNumber = "${currentBuild.number}"
                        for (buildTarget in buildTargets) {
                            if (params[paramNameMap[buildTarget]]) {
                                def platform = getPlatformContext(buildTarget, params.DEVELOPMENT, buildNumber, commit, buildConfig)
                                String link = "https://nexus.cradle.buas.nl/#browse/browse:${nexusRepo}:${buildTarget}%%2F${platform.buildName}"
                                links += "[Download ${platform.description} Build from Nexus](${link});"
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

def createBuildConfig(buildNameMap, buildNameDevMap, outputFolderMap, descriptionMap, unityBuildNameExtensionMap)
{
    return [
        buildNameMap: buildNameMap,
        buildNameDevMap: buildNameDevMap,
        outputFolderMap: outputFolderMap,
        descriptionMap: descriptionMap,
        unityBuildNameExtensionMap: unityBuildNameExtensionMap
    ]
}

// Unity build profile assets live here, named after the raw (pre-sanitized) build name, e.g. "WindowsServerDev.asset"
def getBuildProfilePath(profileName)
{
    return "Assets/Settings/Build Profiles/${profileName}.asset"
}

// resolves the dev/non-dev maps for one target into a single flat, platform-agnostic object
def getPlatformContext(buildTarget, useDev, buildNumber, commit, buildConfig)
{
    def rawBuildName = useDev ? buildConfig.buildNameDevMap[buildTarget] : buildConfig.buildNameMap[buildTarget]
    return [
        target: buildTarget,
        outputFolder: buildConfig.outputFolderMap[buildTarget],
        buildName: sanitizeinput.buildName(rawBuildName, buildNumber, commit, "zip"),
        buildProfilePath: getBuildProfilePath(rawBuildName),
        description: buildConfig.descriptionMap[buildTarget],
        extension: buildConfig.unityBuildNameExtensionMap[buildTarget]
    ]
}

def build(Node, WorkingDir, output, outputFolder, outputFileName, buildProfilePath, unityVersion, discordWebhook)
{
    build job: 'Library/WindowsUnityBuildV2',
    parameters: [
        string(name: 'WORKING_DIR', value: WorkingDir),
        string(name: 'NODE', value: Node),
        string(name: 'DISCORD_WEBHOOK', value: discordWebhook),
        string(name: 'UNITY_VERSION', value: "${unityVersion}"),
        string(name: 'PROJECT_PATH', value: "%CD%"),
        string(name: 'OUTPUT_PATH', value: "%CD%\\${output}\\${outputFolder}\\${outputFileName}"),
        string(name: 'BUILD_PROFILE_PATH', value: buildProfilePath)
    ]
}

def stagesBuildAndUpload(platform, env)
{
    // we swallow any exceptions during the build, zip, and upload stages to ensure the pipeline continues for other build targets

    def prevStageSuccess = true
    stage(platform.target+'Build') {
        try{
            build(
                Node, 
                WorkingDir, 
                outputBase, 
                platform.outputFolder, 
                "${unityBuildName}${platform.extension}", 
                platform.buildProfilePath, 
                unityVersion, 
                discordWebhook)

        } catch (Exception e) {
            prevStageSuccess = false
            catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                error("Build failed for ${platform.target} Exception: ${e.message}")
            }
        }
    }
    stage("Zip${platform.target}Build") {
        try{
            if(prevStageSuccess){
                zip.pack(".\\${outputBase}\\${platform.outputFolder}", platform.buildName)
            }else{
                catchError(buildResult: 'FAILURE', stageResult: 'ABORTED') {
                    error("Previous stage failed for ${platform.target}, skipping zip")
                }
            }
        } catch (Exception e) {
            prevStageSuccess = false
            catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                error("Zip failed for ${platform.target} Exception: ${e.message}")
            }
        }
    }
    stage("Upload${platform.target}Build") {
        try{
            if(prevStageSuccess){
                nexus.upload("${nexusRepo}", platform.buildName, "application/x-zip-compressed", platform.target, 'NEXUS_CREDENTIALS')
            }else{
                catchError(buildResult: 'FAILURE', stageResult: 'ABORTED') {
                    error("Previous stage failed for ${platform.target}, skipping upload")
                }
            }
        } catch (Exception e) {
            prevStageSuccess = false
            catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                error("Upload failed for ${platform.target}")
            }
        }
    }
}