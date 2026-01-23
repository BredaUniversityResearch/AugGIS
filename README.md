## About
Welcome to the public repository for the AugGIS tool. For more information about AugGIS, see the [website](https://cradle.buas.nl/project/auggis/).

Want to learn about the more about the application? Check out the [User guide](https://cradlebuas.fibery.io/Admin/Wiki_Page/How-To-Guide-1179?sharing-key=c140098e-996b-441d-9761-d80dadbc854e).

## Setup
Using AugGIS in a workshop always requires 4 components:
- The AugGIS client, usually running on a VR HMD.
- The AugGIS host, usually running on a pc, server or docker container.
- The data providing service.
- The session manager. While optional, it is recommended to have a service that starts hosts on demand, provides QR codes to clients and manages the session status.

The AugGIS client and host components are part of this repository. For the data service and session manager, two options currently exist:
- AugGIS has been integrated in the [MSP Challenge](https://www.mspchallenge.info/), allowing AugGIS sessions to be created on demand from the MSPC client. A guide for this setup can be found [Here](https://cradlebuas.fibery.io/Admin/Wiki_Page/AugGIS+MSPC-Server-setup-guide-719?sharing-key=7d929c93-3b7e-4614-9403-8e67258df652).
- For other uses we created the AugGIS Config Tool. This allows custom geodata to be loaded, the visualization to be completely configured and sessions to be hosted. The repo and guide for the config tool can be found [Here](https://github.com/BredaUniversityResearch/AugGISConfigTool).

| Name       | Host      | Data provider    | Session management |
| ------------- | ------------- |------------- | ------------- |
| **MSP Challenge** | Docker container | MSPC Server, using MSPC session data | Session creation and management directly from the MSPC Client |
| **AugGIS Config Tool**| Headless application | Config tool, using custom data | Simple session creation |


## Plugins
The project uses the following plugins, which are not included in the repository due to their license and will have to be seperate acquired:
- [Odin Inspector](https://odininspector.com/) V3.3.1.10, required to use this repository.
- [OpenCV for Unity](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088) V2.6.5. Optional, but required for marker based area setup. Without this the table anchors have to be configured manually.
