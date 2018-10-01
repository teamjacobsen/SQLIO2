Run `dotnet publish -c Release -r win7-x64` to publish self contained exe.

# Scanner
Start server listening for incoming connections on `SCANNERPORT`with the command:
```
sqlio2 proxy -l SCANNERPORT
```

# PLC
Start server listenening for incoming connections on `PLCPORT` with the command:
```
sqlio2 proxy -l PLCPORT -f FANPORT
```
Send to all the PLCs currently connected to `PLCPORT` with `sqlio2 client -p FANPORT hexdata`.

# Printer
Send to printer with `sqlio2 client -h PRINTERHOST -p PRINTERPORT -r videojet -t 1000 hexdata` for a transmission with a 1 second timeout.