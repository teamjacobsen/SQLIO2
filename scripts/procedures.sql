CREATE PROCEDURE [dbo].SQLIO_IncomingPacket
@LocalHost nvarchar(256),
@LocalPort int,
@RemoteHost nvarchar(256),
@RemotePort int,
@Request varbinary(2048),
@Reply varbinary(2048) OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
END

GO

CREATE PROCEDURE [dbo].SQLIO_IncomingXML
@LocalHost nvarchar(256),
@LocalPort int,
@RemoteHost nvarchar(256),
@RemotePort int,
@Request xml,
@Reply xml OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
END

GO