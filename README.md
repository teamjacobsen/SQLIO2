# Build
Run `dotnet publish -c Release -r win-x64` to publish a self contained exe.

# Run
## Scanner
Start server listening for incoming connections on `SCANNERPORT`with the command:
```
sqlio2 proxy -l SCANNERPORT
```

## PLC
Start server listenening for incoming connections on `PLCPORT` with the command:
```
sqlio2 proxy -l PLCPORT -f FANPORT
```
Send to all the PLCs currently connected to `PLCPORT` with the command:
```
sqlio2 client -p FANPORT hexdata
```

## Printer
Send to printer with the command:
```
sqlio2 client -h PRINTERHOST -p PRINTERPORT -r videojet -t 1000 hexdata
```
for a transmission with a 1 second timeout.

## Weight
Establish connection to weight with the command:
```
sqlio2 proxy -h WEIGHTHOST -p WEIGHTPORT -r sc500 -f FANPORT
```
Send alive through connection:
```
sqlio2 client -p FANPORT -f alive.xml
```