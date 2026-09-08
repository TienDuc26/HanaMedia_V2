-- =========================================================================
-- DATABASE PATCH: Add Module 11 Contract Workflow fields to bookings table
-- =========================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('bookings') AND name = 'contract_status')
BEGIN
    ALTER TABLE bookings ADD contract_status NVARCHAR(50) NULL DEFAULT 'cho_duyet';
    ALTER TABLE bookings ADD contract_approved_at DATETIME2 NULL;
    ALTER TABLE bookings ADD contract_approved_by_id INT NULL;
    ALTER TABLE bookings ADD contract_signed_at DATETIME2 NULL;
    ALTER TABLE bookings ADD contract_signed_by_id INT NULL;
    ALTER TABLE bookings ADD rejection_reason NVARCHAR(1000) NULL;

    ALTER TABLE bookings ADD CONSTRAINT FK_bookings_contract_approved_by FOREIGN KEY (contract_approved_by_id) REFERENCES employees(id);
    ALTER TABLE bookings ADD CONSTRAINT FK_bookings_contract_signed_by FOREIGN KEY (contract_signed_by_id) REFERENCES employees(id);

    -- Update existing bookings to default 'cho_duyet' contract status
    UPDATE bookings SET contract_status = 'cho_duyet' WHERE contract_status IS NULL;
END;
GO
