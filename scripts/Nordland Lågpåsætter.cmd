CHCP 65001
TITLE Nordland Lågpåsætter

MKDIR log

START /B "S1" sqlio2 proxy -l 8211 > log\LpNoS1.log
START /B "S2" sqlio2 proxy -l 8212 > log\LpNoS2.log
START /B "S3" sqlio2 proxy -l 8213 > log\LpNoS3.log
START /B "S4" sqlio2 proxy -l 8214 > log\LpNoS4.log
START /B "C1/S1" sqlio2 proxy -l 8221 -f 18221 > log\LpNoPlcS1.log
START /B "C1/S2" sqlio2 proxy -l 8222 -f 18222 > log\LpNoPlcS2.log
START /B "C1/S3" sqlio2 proxy -l 8223 -f 18223 > log\LpNoPlcS3.log
START /B "C1/S4" sqlio2 proxy -l 8224 -f 18224 > log\LpNoPlcS4.log