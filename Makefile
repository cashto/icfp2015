all : 
	msbuild solve\solve.sln /property:Configuration=Release
	cp solve/bin/Release/* .
	mv solve.exe play_icfp2015.exe
	mv solve.exe.config play_icfp2015.exe.config