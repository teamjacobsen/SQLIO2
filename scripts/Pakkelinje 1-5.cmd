SET SQLIO="C:\Source\SqlIO2\src\SQLIO2\bin\Release\netcoreapp2.1\win10-x64\SQLIO2.exe"

CHCP 65001
TITLE Pakkelinje 1-5

START /B "BS" %SQLIO% proxy -l 8077 > Pa15Bs.log
START /B "NP" %SQLIO% proxy -l 8078 -f 18078 > Pa15Plc.log
START /B "NP/NB" %SQLIO% proxy -l 8088 > Pa15PlcNb.log
START /B "NP/NM" %SQLIO% proxy -l 8089 > Pa15PlcNm.log
START /B "MS" %SQLIO% proxy -l 8080 > Pa15Ms.log