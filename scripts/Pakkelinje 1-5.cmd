SET SQLIO="C:\Source\SqlIO2\src\SQLIO2\bin\Release\netcoreapp2.1\win10-x64\SQLIO2.exe"

CHCP 65001
TITLE Pakkelinje 1-5

START /B "SB" %SQLIO% proxy -l 8077 > SB.log
START /B "NP" %SQLIO% proxy -l 8078 -f 18078 > NP.log
START /B "NP-NB" %SQLIO% proxy -l 8088 > NP-NB.log
START /B "NP-NM" %SQLIO% proxy -l 8089 > NP-NM.log
START /B "MS" %SQLIO% proxy -l 8080 > MS.log