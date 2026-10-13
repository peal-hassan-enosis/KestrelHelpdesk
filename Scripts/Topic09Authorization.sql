-- Topic 09: ticket authorization schema update
-- Run this once against the KestrelHelpdesk database after pulling Topic-09 changes.

IF COL_LENGTH('Tickets', 'AssignedToUserId') IS NULL
BEGIN
    ALTER TABLE Tickets ADD AssignedToUserId nvarchar(450) NULL;
END;
GO

UPDATE t
SET CreatedBy = u.Id
FROM Tickets t
INNER JOIN AspNetUsers u ON
    (t.CreatedBy = 'Alice Employee' AND u.Email = 'employee@kestrelhelpdesk.com') OR
    (t.CreatedBy = 'Bob Agent' AND u.Email = 'agent@kestrelhelpdesk.com') OR
    (t.CreatedBy = 'Charlie Manager' AND u.Email = 'manager@kestrelhelpdesk.com') OR
    (t.CreatedBy = 'employee@kestrelhelpdesk.com' AND u.Email = 'employee@kestrelhelpdesk.com') OR
    (t.CreatedBy = 'agent@kestrelhelpdesk.com' AND u.Email = 'agent@kestrelhelpdesk.com') OR
    (t.CreatedBy = 'manager@kestrelhelpdesk.com' AND u.Email = 'manager@kestrelhelpdesk.com');
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Tickets_AssignedToUserId'
      AND object_id = OBJECT_ID('Tickets'))
BEGIN
    CREATE INDEX IX_Tickets_AssignedToUserId ON Tickets(AssignedToUserId);
END;
GO
