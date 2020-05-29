#!make

release:
	msbuild ./TreeDepth/TreeDepth.csproj -property:Configuration=Release
	cp ./TreeDepth/bin/x64/Release/Treedepth.exe ./Treedepth.exe

