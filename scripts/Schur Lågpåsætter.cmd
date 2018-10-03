CHCP 65001
TITLE Schur Lågpåsætter

MKDIR log

START /B "S7" sqlio2 proxy -l 8217 > log\LpScS7.log
START /B "SP" sqlio2 proxy -l 8073 -f 18073 > log\LpScPlc.log