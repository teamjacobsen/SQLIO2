SET SQLIO="C:\Source\SqlIO2\src\SQLIO2\bin\Release\netcoreapp2.1\win10-x64\SQLIO2.exe"

CHCP 65001
TITLE Nordland Lågpåsætter

START /B "S1" %SQLIO% proxy -l 8211 > LpNoS1.log
START /B "S2" %SQLIO% proxy -l 8212 > LpNoS2.log
START /B "S3" %SQLIO% proxy -l 8213 > LpNoS3.log
START /B "S4" %SQLIO% proxy -l 8214 > LpNoS4.log
START /B "C1/S1" %SQLIO% proxy -l 8221 -f 18221 > LpNoPlcS1.log
START /B "C1/S2" %SQLIO% proxy -l 8222 -f 18222 > LpNoPlcS2.log
START /B "C1/S3" %SQLIO% proxy -l 8223 -f 18223 > LpNoPlcS3.log
START /B "C1/S4" %SQLIO% proxy -l 8224 -f 18224 > LpNoPlcS4.log