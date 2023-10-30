# CelesteBot
A complete system to train and run a Reinforcement Learning agent that can play Celeste, using PPO. The system has a somewhat complex architecture, interfacing through Everest to retrieve Celeste game data as well as send inputs to the game, and interfacing with Python to run the actual RL algorithm through Ray RL Lib. The Celeste client uses PythonNET to interface with a Ray client instance which interfaces with a centralized Ray server that can handle training several game clients simultaneously. Currently being developed by Ashvio, but the codebase is from from sc2ad's original work in 2019.
## Getting Started
These instructions should help you get started running and developing for CelesteBot.
### Installing
CelesteBot uses [Everest](https://everestapi.github.io/), you should follow the [installation instructions for Everest](https://everestapi.github.io/#installing-everest). After installation is complete:
1. Clone the CelesteBot repo to anywhere you desire.
2. Open `CelesteBot/CelesteBot-Everest-Interop/CelesteBot-2023.csproj` in your favorite C# IDE (VisualStudio works best).
3. Build the project to `CelesteBot/CelesteBot-Everest-Interop/bin/Debug/CelesteBot-Everest-Interop.dll` (default on VisualStudio).
4. `move_dll.bat` will run automatically as a post build action to move all required files to your Celeste directory.

After that, you should be good to open Celeste and run CelesteBot.
### References
When developing for CelesteBot, you should ensure you have the following references present:
```
Celeste:
    Celeste
    FNA
    MMHOOK_Celeste
    Mono.Cecil
    MonoMod
    MonoMod.RuntimeDetour
    MonoMod.Utils
    YamlDotNet
.NET:
    Microsoft.CSharp
    System
    System.Core
    System.Runtime.Serialization
    System.Xml
```
### Controls
| Key | Function |
| --- | --- |
| \ | Begin training |
| , | Stop training, reset level |
| ' | Load generation specified by CelesteBot Mod setting "Checkpoint to Load" |
| A | Toggle fitness append mode |
| Space | Add fitness checkpoint to fitness.fit (Only in fitness append mode) |
| N | Hide brain |
| Shift + N | Show brain |
