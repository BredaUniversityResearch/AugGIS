//@Library('CradleSharedLibrary') _ // Loaded implicitly

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
String gitHubBranch = "dev"
String discordFriendlyName = "Auggis"

String nexusRepo = "MSP_ProceduralOceanViewUnity-Main"

String unityBuildName = "Auggis"
String unityVersion = "6000.3.6f1"

String output = "Output"

// "constants", used for the map keys
String windows = "Windows"
String android = "Android"
String unityServer = "UnityServer"
def buildTargets = [windows, android, unityServer]

// maps
def buildNameMap = [:]
def buildNameDevMap = [:]
def outputFolderMap = [:]
def outputFolderDevMap = [:]
def paramNameMap = [:]
def descriptionMap = [:]
def unityBuildNameExtensionMap = [:]

// map values
buildNameMap[windows] = "Windows"
buildNameMap[android] = "Android"
buildNameMap[unityServer] = "UnityServer"
buildNameDevMap[windows] = "Windows-Dev"
buildNameDevMap[android] = "Android-Dev"
buildNameDevMap[unityServer] = "UnityServer-Dev"
outputFolderMap[windows] = "CurrentWinBuild"
outputFolderMap[android] = "CurrentAndroidBuild"
outputFolderMap[unityServer] = "CurrentUnityServerBuild"
outputFolderDevMap[windows] = "CurrentWinDevBuild"
outputFolderDevMap[android] = "CurrentAndroidDevBuild"
outputFolderDevMap[unityServer] = "CurrentUnityServerDevBuild"
paramNameMap[windows] = "BUILD_WINDOWS"
paramNameMap[android] = "BUILD_ANDROID"
paramNameMap[unityServer] = "BUILD_UNITY_SERVER"
descriptionMap[windows] = "Windows"
descriptionMap[android] = "Android"
descriptionMap[unityServer] = "Unity Server"
unityBuildNameExtensionMap[windows] = '.exe'
unityBuildNameExtensionMap[android] = '.apk'
unityBuildNameExtensionMap[unityServer] = ''

properties([
    parameters([
        booleanParam(name: 'DEVELOPMENT', defaultValue: false, description: 'Development build?'),
        booleanParam(name: paramNameMap[windows], defaultValue: false, description: "Make a ${descriptionMap[windows]} build"),
        booleanParam(name: paramNameMap[android], defaultValue: true, description: "Make a ${descriptionMap[android]} build"),
        booleanParam(name: paramNameMap[unityServer], defaultValue: true, description: "Make a ${descriptionMap[unityServer]} build"),
    ])
])

String discordWebhook = 'POV_DISCORD_WEBHOOK'

// End of Config section

Boolean cleanupBefore = false
Boolean cleanupAfter = true

String commit = ""
String messageIfStageFailure = ""
try {
    stage('Clone') {
        messageIfStageFailure = "Failed to clean workspace"
        if (cleanupBefore) {
            node(Node) {
                dir(WorkingDir) {
                    script {
                        deleteDir()
                    }
                }
                cleanWs()
            }
        }
        messageIfStageFailure = "Failed to clone repositories"
        node(Node) {
            git.checkoutWithSubModules("https://github.com/${gitHubRepo}", "${gitHubBranch}", 'CRADLE_WEBMASTER_CREDENTIALS')
            commit = git.fetchCommitHash('CRADLE_WEBMASTER_CREDENTIALS')
        }
    }

    stage('Build') {
        node(Node) {
            script {
                def context = createContext(buildNameMap, buildNameDevMap, outputFolderMap, outputFolderDevMap, descriptionMap)
                String env = params.DEVELOPMENT ? "Dev" : ""
                String buildNumber = "${currentBuild.number}"
                for (buildTarget in buildTargets) {
                    def (outputFolder, buildName, tempMessageIfStageFailure) = getBuildDetails(buildTarget, params.DEVELOPMENT, buildNumber, commit, context)
                    messageIfStageFailure = tempMessageIfStageFailure
                    if (params[paramNameMap[buildTarget]]) {
                        stage(buildTarget+'Build') {
                            build(Node, WorkingDir, output, outputFolder, "${unityBuildName}${unityBuildNameExtensionMap[buildTarget]}", "BuildUtility.${buildTarget}${env}Builder", unityVersion, discordWebhook)
                        }
                        stage("Zip${buildTarget}Build") {
                            zip.pack(".\\${output}\\${outputFolder}", buildName)
                        }
                        stage("Upload${buildTarget}Build") {
                            nexus.upload("${nexusRepo}", buildName, "application/x-zip-compressed", buildTarget, 'NEXUS_CREDENTIALS')
                        }
                    } else {
                        stage(buildTarget+'Build') {
                            catchError(buildResult: 'SUCCESS', stageResult: 'ABORTED') {
                                error(descriptionMap[buildTarget]+' Build was skipped')
                            }
                        }
                        stage("Zip${buildTarget}Build") {
                            catchError(buildResult: 'SUCCESS', stageResult: 'ABORTED') {
                                error(descriptionMap[buildTarget]+' Zip was skipped')
                            }
                        }
                        stage("Upload${buildTarget}Build") {
                            catchError(buildResult: 'SUCCESS', stageResult: 'ABORTED') {
                                error(descriptionMap[buildTarget]+' Upload was skipped')
                            }
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
    node(Node) {
        script {
            discord.failed(discordWebhook, "${discordFriendlyName}", "${messageIfStageFailure}\n\n${e}")
        }
    }
    throw (e)
} finally {
    try {
        stage('Report-Results') {
            node(Node) {
                script {
                    switch (currentBuild.result) {
                        case "UNSTABLE":
                            echo "Build was unstable"
                            break
                        case "FAILURE":
                            echo "Build failed"
                            break
                        case "ABORTED":
                            echo "Build was aborted"
                            break
                        default: // case "SUCCESS":
                            if (currentBuild.result != 'SUCCESS') {
                                echo "Unknown result, assuming build was successful"
                            }
                            def context = createContext(buildNameMap, buildNameDevMap, outputFolderMap, outputFolderDevMap, descriptionMap)
                            String links = ""
                            String discordMessageTitle = "${discordFriendlyName}" + (params.DEVELOPMENT ? " (Dev)" : "")
                            String env = params.DEVELOPMENT ? "Dev" : ""
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
                }
            }
        }
    } catch (InterruptedException e) {
        catchError(buildResult: 'ABORTED', stageResult: 'ABORTED') {
            error()
        }
        throw (e)
    } catch (Exception e) {
        catchError(buildResult: currentBuild.currentResult, stageResult: 'FAILURE') {
            error()
        }
        throw (e)
    } finally {
        stage('Cleanup') {
            if (cleanupAfter) {
                try {
                    node(Node) {
                        dir(WorkingDir) {
                            script {
                                deleteDir()
                            }
                        }
                        cleanWs()
                    }
                } catch (Exception e) {
                    echo "Unexpected failure during cleanup, retrying once..."
                    node(Node) {
                        dir(WorkingDir) {
                            script {
                                deleteDir()
                            }
                        }
                        cleanWs()
                    }
                    throw (e)
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
    def outputFolder = getFolder(buildTarget, useDev, context)
    def buildName = getBuildName(buildTarget, useDev, buildNumber, commit, context)
    def messageIfStageFailure = 'Failed to build '+context.descriptionMap[buildTarget]
    return [outputFolder, buildName, messageIfStageFailure]
}

def getBuildName(buildTarget, useDev, buildNumber, commit, context)
{
    return sanitizeinput.buildName(getValue(buildTarget, context.buildNameMap, context.buildNameDevMap, useDev), buildNumber, commit, "zip")
}

def getFolder(buildTarget, useDev, context)
{
    return getValue(buildTarget, context.outputFolderMap, context.outputFolderDevMap, useDev)
}

def getValue(buildTarget, values, devValues, useDev)
{
    return useDev ? devValues[buildTarget] : values[buildTarget]
}

def build(Node, WorkingDir, output, outputFolder, buildName, buildMethod, unityVersion, discordWebhook)
{
    build job: 'Library/WindowsUnityBuild',
    parameters: [
        string(name: 'NODE', value: Node),
        string(name: 'WORKING_DIR', value: WorkingDir),
        string(name: 'UNITY_VERSION', value: "${unityVersion}"),
        string(name: 'PROJECTPATH', value: "%CD%"),
        string(name: 'EXPORTPATH', value: "%CD%\\${output}\\${outputFolder}\\${buildName}"),
        string(name: 'BUILD_NAME', value: buildName),
        string(name: 'BUILD_METHOD', value: buildMethod),
        string(name: 'DISCORD_WEBHOOK', value: discordWebhook)
    ]
}
