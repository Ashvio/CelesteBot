echo %cd%

copy "C:\projects\CelesteBot\CelesteBot-2023\bin\Debug\net7.0\CelesteBot_2023.dll" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\Mods\CelesteBot\Code" ;
copy "C:\Users\Ashvin\.nuget\packages\pythonnet\3.0.3\lib\netstandard2.0\Python.Runtime.dll" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\"  ;
copy "C:\Users\Ashvin\.nuget\packages\desktop.robot\1.5.0\lib\net6.0\Desktop.Robot.dll" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\"  ;

copy "everest.yaml" "C:\Program Files (x86)\Steam\steamapps\common\Celeste\Mods\CelesteBot\" 
robocopy /MIR "python_rl " "C:\Program Files (x86)\Steam\steamapps\common\Celeste\python_rl\ " 
exit 0
:: "C:\Program Files (x86)\Steam\steamapps\common\Celeste\Celeste.exe"