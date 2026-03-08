-- Fix role_name values to match original Node.js init_db.sql (1-based IDs)
-- This corrects any damage done by the previous 0-based migration

INSERT INTO roles(id, role_name) VALUES
    (1, 'user'),
    (2, 'admin_divisi'),
    (3, 'super_admin'),
    (4, 'super_duper_admin')
ON CONFLICT (id) DO UPDATE SET role_name = EXCLUDED.role_name;

-- Remove the erroneous id=0 entry if it exists
DELETE FROM roles WHERE id = 0 AND NOT EXISTS (SELECT 1 FROM users WHERE role_id = 0);

-- Fix any users that may have been assigned role_id=0
UPDATE users SET role_id = 1 WHERE role_id = 0;

-- Update default
ALTER TABLE users ALTER COLUMN role_id SET DEFAULT 1;
