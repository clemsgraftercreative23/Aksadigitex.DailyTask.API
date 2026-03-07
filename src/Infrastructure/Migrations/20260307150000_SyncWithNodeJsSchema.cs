using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncWithNodeJsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // 1. USERS TABLE — drop high_value_threshold, add new columns
            // ============================================================
            migrationBuilder.Sql(@"
                ALTER TABLE users DROP COLUMN IF EXISTS high_value_threshold;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS last_mfa_verified_at TIMESTAMPTZ;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS last_active_at TIMESTAMPTZ;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS notif_threshold_min NUMERIC DEFAULT 0;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS notif_threshold_max NUMERIC DEFAULT 1000000;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS urgency_email VARCHAR(255);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS enable_urgensi BOOLEAN DEFAULT TRUE;
            ");

            // ============================================================
            // 2. DIRECTOR_POSITIONS — add parent_id (self-reference)
            // ============================================================
            migrationBuilder.Sql(@"
                ALTER TABLE director_positions 
                    ADD COLUMN IF NOT EXISTS parent_id INT REFERENCES director_positions(id) ON DELETE SET NULL;
            ");

            // ============================================================
            // 3. DIRECTOR_SUBORDINATES — new join table
            // ============================================================
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS director_subordinates (
                    root_id INT REFERENCES director_positions(id) ON DELETE CASCADE,
                    subordinate_id INT REFERENCES director_positions(id) ON DELETE CASCADE,
                    PRIMARY KEY (root_id, subordinate_id)
                );
            ");

            // ============================================================
            // 4. DIRECTOR_USERS — drop position, fix company_id, add cols
            // ============================================================
            migrationBuilder.Sql(@"
                ALTER TABLE director_users DROP COLUMN IF EXISTS ""position"";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'director_users'
                          AND column_name = 'company_id'
                          AND data_type = 'character varying'
                    ) THEN
                        ALTER TABLE director_users
                            ALTER COLUMN company_id TYPE INT
                            USING CASE WHEN company_id ~ '^\d+$' THEN company_id::INT ELSE NULL END;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'fk_director_users_company'
                          AND table_name = 'director_users'
                    ) THEN
                        ALTER TABLE director_users
                            ADD CONSTRAINT fk_director_users_company
                            FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE SET NULL;
                    END IF;
                END $$;

                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS last_mfa_verified_at TIMESTAMPTZ;
                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS last_active_at TIMESTAMPTZ;
                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS notif_threshold_min NUMERIC DEFAULT 0;
                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS notif_threshold_max NUMERIC DEFAULT 10000000;
                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS urgency_email VARCHAR(255);
                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS enable_urgensi BOOLEAN DEFAULT TRUE;
            ");

            // ============================================================
            // 5. REPORT_PERIODS — deadline TIME→DATE, add is_active
            // ============================================================
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'report_periods'
                          AND column_name = 'deadline'
                          AND data_type = 'time without time zone'
                    ) THEN
                        ALTER TABLE report_periods DROP COLUMN deadline;
                        ALTER TABLE report_periods ADD COLUMN deadline DATE;
                    END IF;
                END $$;

                ALTER TABLE report_periods ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT TRUE;
            ");

            // ============================================================
            // 6. DAILY_REPORT — status VARCHAR(50), drop CHECK, timestamptz
            // ============================================================
            migrationBuilder.Sql(@"
                ALTER TABLE daily_report ALTER COLUMN status TYPE VARCHAR(50);
                ALTER TABLE daily_report ALTER COLUMN status SET DEFAULT 'draft';

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.check_constraints
                        WHERE constraint_name = 'daily_report_status_check'
                    ) THEN
                        ALTER TABLE daily_report DROP CONSTRAINT daily_report_status_check;
                    END IF;
                END $$;

                ALTER TABLE daily_report 
                    ALTER COLUMN created_at TYPE TIMESTAMPTZ USING created_at AT TIME ZONE 'UTC';
                ALTER TABLE daily_report 
                    ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP;
            ");

            // ============================================================
            // 7. DIRECTOR_REPORTS — same changes as daily_report
            // ============================================================
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'director_reports') THEN
                        ALTER TABLE director_reports ALTER COLUMN status TYPE VARCHAR(50);
                        ALTER TABLE director_reports ALTER COLUMN status SET DEFAULT 'draft';

                        IF EXISTS (
                            SELECT 1 FROM information_schema.check_constraints
                            WHERE constraint_name = 'director_reports_status_check'
                        ) THEN
                            ALTER TABLE director_reports DROP CONSTRAINT director_reports_status_check;
                        END IF;

                        ALTER TABLE director_reports
                            ALTER COLUMN created_at TYPE TIMESTAMPTZ USING created_at AT TIME ZONE 'UTC';
                        ALTER TABLE director_reports
                            ALTER COLUMN created_at SET DEFAULT CURRENT_TIMESTAMP;
                    END IF;
                END $$;
            ");

            // ============================================================
            // 8. NOTIFICATIONS — drop FK & CHECK constraints
            // ============================================================
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'notifications_reference_id_fkey'
                          AND table_name = 'notifications'
                    ) THEN
                        ALTER TABLE notifications DROP CONSTRAINT notifications_reference_id_fkey;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.check_constraints
                        WHERE constraint_name = 'notifications_sender_type_check'
                    ) THEN
                        ALTER TABLE notifications DROP CONSTRAINT notifications_sender_type_check;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.check_constraints
                        WHERE constraint_name = 'notifications_type_check'
                    ) THEN
                        ALTER TABLE notifications DROP CONSTRAINT notifications_type_check;
                    END IF;
                END $$;
            ");

            // ============================================================
            // 9. DIRECTOR_NOTIFICATIONS — drop CHECK constraints
            // ============================================================
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'director_notifications') THEN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.check_constraints
                            WHERE constraint_name = 'director_notifications_sender_type_check'
                        ) THEN
                            ALTER TABLE director_notifications DROP CONSTRAINT director_notifications_sender_type_check;
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM information_schema.check_constraints
                            WHERE constraint_name = 'director_notifications_type_check'
                        ) THEN
                            ALTER TABLE director_notifications DROP CONSTRAINT director_notifications_type_check;
                        END IF;
                    END IF;
                END $$;
            ");

            // ============================================================
            // 10. DROP Products tables (leftover from EF scaffold)
            // ============================================================
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""Products"";
                DROP TABLE IF EXISTS products;
            ");

            // ============================================================
            // 11. SEED — roles & director_roles
            // ============================================================
            migrationBuilder.Sql(@"
                INSERT INTO roles (role_name) VALUES
                    ('super_duper_admin'), ('super_admin'), ('admin_divisi'), ('user')
                ON CONFLICT (role_name) DO NOTHING;

                INSERT INTO director_roles (role_name) VALUES
                    ('super_duper_admin'), ('super_admin'), ('user')
                ON CONFLICT (role_name) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse user column changes
            migrationBuilder.Sql(@"
                ALTER TABLE users DROP COLUMN IF EXISTS last_mfa_verified_at;
                ALTER TABLE users DROP COLUMN IF EXISTS last_active_at;
                ALTER TABLE users DROP COLUMN IF EXISTS notif_threshold_min;
                ALTER TABLE users DROP COLUMN IF EXISTS notif_threshold_max;
                ALTER TABLE users DROP COLUMN IF EXISTS urgency_email;
                ALTER TABLE users DROP COLUMN IF EXISTS enable_urgensi;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS high_value_threshold NUMERIC;
            ");

            // Reverse director_positions change
            migrationBuilder.Sql(@"
                ALTER TABLE director_positions DROP COLUMN IF EXISTS parent_id;
            ");

            // Drop director_subordinates
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS director_subordinates;
            ");

            // Reverse director_users changes
            migrationBuilder.Sql(@"
                ALTER TABLE director_users DROP COLUMN IF EXISTS last_mfa_verified_at;
                ALTER TABLE director_users DROP COLUMN IF EXISTS last_active_at;
                ALTER TABLE director_users DROP COLUMN IF EXISTS notif_threshold_min;
                ALTER TABLE director_users DROP COLUMN IF EXISTS notif_threshold_max;
                ALTER TABLE director_users DROP COLUMN IF EXISTS urgency_email;
                ALTER TABLE director_users DROP COLUMN IF EXISTS enable_urgensi;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'fk_director_users_company'
                    ) THEN
                        ALTER TABLE director_users DROP CONSTRAINT fk_director_users_company;
                    END IF;
                END $$;

                ALTER TABLE director_users ADD COLUMN IF NOT EXISTS ""position"" VARCHAR(50);
            ");

            // Reverse report_periods changes
            migrationBuilder.Sql(@"
                ALTER TABLE report_periods DROP COLUMN IF EXISTS is_active;
                ALTER TABLE report_periods DROP COLUMN IF EXISTS deadline;
                ALTER TABLE report_periods ADD COLUMN IF NOT EXISTS deadline TIME NOT NULL DEFAULT '00:00:00';
            ");

            // Reverse daily_report changes
            migrationBuilder.Sql(@"
                ALTER TABLE daily_report ALTER COLUMN status TYPE VARCHAR(20);
                ALTER TABLE daily_report ALTER COLUMN created_at TYPE TIMESTAMP USING created_at AT TIME ZONE 'UTC';
            ");
        }
    }
}
