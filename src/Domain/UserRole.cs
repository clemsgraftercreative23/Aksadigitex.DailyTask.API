namespace Domain;

/// <summary>
/// Mendefinisikan role yang tersedia dalam sistem
/// </summary>
public enum UserRole
{
    /// <summary>User biasa dengan akses terbatas</summary>
    User = 1,
    
    /// <summary>Admin divisi - mengelola laporan dalam divisinya</summary>
    AdminDivisi = 2,
    
    /// <summary>Super admin - mengelola semua laporan dan user</summary>
    SuperAdmin = 3,
    
    /// <summary>Super duper admin - akses penuh ke sistem</summary>
    SuperDuperAdmin = 4
}
