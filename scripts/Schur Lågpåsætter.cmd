SET SQLIO="C:\Source\SqlIO2\src\SQLIO2\bin\Release\netcoreapp2.1\win10-x64\SQLIO2.exe"

CHCP 65001
TITLE Schur Lågpåsætter

START /B "S2" %SQLIO% proxy -l 8212 > S2.log
START /B "S7" %SQLIO% proxy -l 8217 > S7.log
START /B "SP" %SQLIO% proxy -l 8073 -f 18073 > SP.log