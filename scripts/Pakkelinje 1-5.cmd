CHCP 65001
TITLE Pakkelinje 1-5

MKDIR log

START /B "BS" sqlio2 proxy -l 8077 > log\Pa15Bs.log
START /B "NP" sqlio2 proxy -l 8078 > log\Pa15Plc.log
START /B "NP/NB" sqlio2 proxy -l 8088 -f 18088 > log\Pa15PlcNb.log
START /B "NP/NM" sqlio2 proxy -l 8089 -f 18089 > log\Pa15PlcNm.log
START /B "MS" sqlio2 proxy -l 8080 > log\Pa15Ms.log