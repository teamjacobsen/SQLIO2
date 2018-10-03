CHCP 65001
TITLE Pakkelinje 6

MKDIR log

START /B "S5" sqlio2 proxy -l 8215 > log\Pa06Bs.log
START /B "C2" sqlio2 proxy -l 8231 > log\Pa06Plc.log
START /B "C2/NB" sqlio2 proxy -l 8232 -f 18232 > log\Pa06PlcNb.log
START /B "C2/NM" sqlio2 proxy -l 8233 -f 18233 > log\Pa06PlcNm.log
START /B "S6" sqlio2 proxy -l 8235 > log\Pa06Ms.log