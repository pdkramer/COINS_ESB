# COINS_ESB
This project contains the source code for the interface between Apex, written by Vulcan Software LLC, and COINS OA.  Web services are used to receive data from COINS and to send purchase orders to COINS.  P/O amendments are written to a CSV file.

Data to be received from COINS is sent to a server running the COINS_ESB program.  The COINSImport program then reads the data into Apex from this server.

Data is sent to COINS using the ApexCOINESB program.  This program also creates the P/O amendments file.

### ApexCOINESB
This is a Windows Presentation Foundation program using LINQ to SQL to talk to the Apex database, and Windows Communications Foundation to communicate with the COINS server.

It sends new and amended P/Os to COINS.

### COINS_ESB
This is a .NET Core 2.2 program intended to be installed on a Windows IIS server.  The .NET Core runtime needs to be installed on the server to run this program.

It is essentially a "catcher's mitt", ready to catch any XML sent to it from the COINS server to be passed on to Apex (via the COINSImport program).

### COINSImport
COINSImport was written as a command line utility so that it can be easily scheduled by IT to run at specific times.  It communicates with the server running COINS_ESB to download new data being sent to Apex from COINS, and updates the Apex database.

### COINSInspector
This simple WPF utilty will look at the first batch of data queued up on the COINS_ESB server.  It "pretty prints" the XML so that it is more easily readable.  It does not affect the data in any way.

This is useful if there are questions about what data is being sent to Apex, and was used in the initial programming to generate c# classes to deserialize the XML.

### ESB Configuration Notes
Notes about configuring COINS ESB to interface with Apex are available [here](ESB_Config.pdf).

-- *Phil*
