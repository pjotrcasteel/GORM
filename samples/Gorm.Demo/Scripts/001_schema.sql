IF OBJECT_ID(N'[dbo].[CharacteristicSpecificationMapsIntoCharacteristicSpecifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CharacteristicSpecificationMapsIntoCharacteristicSpecifications] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Payload] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_CharacteristicSpecificationMapsIntoCharacteristicSpecifications] PRIMARY KEY ([Id])
    )
    AS EDGE;
END
GO

IF OBJECT_ID(N'[dbo].[CharacteristicSpecifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CharacteristicSpecifications] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Payload] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_CharacteristicSpecifications] PRIMARY KEY ([Id])
    )
    AS NODE;
END
GO
