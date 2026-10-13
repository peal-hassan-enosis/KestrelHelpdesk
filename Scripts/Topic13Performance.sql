-- Topic 13: rendering performance seed data
-- Run this once against the KestrelHelpdesk database after pulling Topic 13 changes.
-- The script tops the Tickets table up to 10,000 rows instead of inserting
-- another 10,000 rows when it is run again.

SET NOCOUNT ON;

DECLARE @CurrentCount int = (SELECT COUNT(*) FROM Tickets);
DECLARE @TargetCount int = 10000;
DECLARE @RowsToInsert int = @TargetCount - @CurrentCount;

IF @RowsToInsert <= 0
BEGIN
    PRINT 'Tickets table already contains 10,000 or more rows. No rows inserted.';
    RETURN;
END;

DECLARE @EmployeeId nvarchar(450) = (
    SELECT TOP (1) Id
    FROM AspNetUsers
    WHERE Email = 'employee@kestrelhelpdesk.com'
);

DECLARE @AgentId nvarchar(450) = (
    SELECT TOP (1) Id
    FROM AspNetUsers
    WHERE Email = 'agent@kestrelhelpdesk.com'
);

DECLARE @ManagerId nvarchar(450) = (
    SELECT TOP (1) Id
    FROM AspNetUsers
    WHERE Email = 'manager@kestrelhelpdesk.com'
);

IF @EmployeeId IS NULL OR @AgentId IS NULL OR @ManagerId IS NULL
BEGIN
    THROW 50001, 'Topic 13 seed requires the Employee, Agent, and Manager users to exist first.', 1;
END;

;WITH Numbers AS
(
    SELECT TOP (@RowsToInsert)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Number
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO Tickets
(
    Title,
    Description,
    Priority,
    Category,
    Status,
    CreatedBy,
    AssignedToUserId,
    CreatedAt
)
SELECT
    CONCAT('Performance Test Ticket ', Number),
    CONCAT(
        'Topic 13 rendering performance test ticket #', Number,
        '. This record is generated to measure list rendering, virtualization, and search performance.'
    ),
    (Number - 1) % 4, -- Low, Medium, High, Critical
    (Number - 1) % 4, -- Hardware, Software, Network, Access
    (Number - 1) % 3, -- Open, InProgress, Resolved
    CASE
        WHEN Number % 10 < 6 THEN @EmployeeId
        WHEN Number % 10 < 9 THEN @AgentId
        ELSE @ManagerId
    END,
    CASE
        WHEN Number % 3 = 0 THEN @AgentId
        ELSE NULL
    END,
    DATEADD(MINUTE, -Number, SYSUTCDATETIME())
FROM Numbers;

DECLARE @TotalCount int = (SELECT COUNT(*) FROM Tickets);

PRINT CONCAT('Inserted ', @RowsToInsert, ' performance test tickets.');
PRINT CONCAT('Total tickets: ', @TotalCount);
GO
