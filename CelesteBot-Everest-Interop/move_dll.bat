copy "bin\Debug\CelesteBot-Everest-Interop.dll" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\Mods\CelesteBot\Code";
copy "C:\Users\Ashvin\miniconda3\envs\CelesteBot\Lib\site-packages\pythonnet\runtime\Python.Runtime.dll" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\";

copy "..\everest.yaml" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\Mods\CelesteBot\";
robocopy "../../rl_client\ " "C:\Program Files (x86)\Steam\steamapps\common\Celeste\rl_client\ " /E