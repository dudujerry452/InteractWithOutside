@echo off

REM Location of the Bin64 folder of the Space Engineers game (SpaceEngineers.exe)
mklink /J Bin64 "F:\SteamLibrary\steamapps\common\SpaceEngineers\Bin64"

REM Location of the local Torch instance (Torch.Server.exe and DedicatedServer64 folder)
mklink /J Torch "F:\Torch"

pause
