SET SQLIO="C:\Source\SqlIO2\src\SQLIO2\bin\Release\netcoreapp2.1\win10-x64\SQLIO2.exe"

CHCP 65001
TITLE Pakkelinje 6

START /B "S5" %SQLIO% proxy -l 8215 > Pa06Bs.log
START /B "C2" %SQLIO% proxy -l 8231 -f 18231 > Pa06Plc.log
START /B "C2/NB" %SQLIO% proxy -l 8232 > Pa06PlcNb.log
START /B "C2/NM" %SQLIO% proxy -l 8233 > Pa06PlcNm.log
START /B "S6" %SQLIO% proxy -l 8235 > Pa06Ms.log