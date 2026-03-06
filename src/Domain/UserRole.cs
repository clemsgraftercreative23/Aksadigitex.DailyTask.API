namespace Domain;

/// <summary>
/// Mendefinisikan role yang tersedia dalam sistem
/// </summary>
public enum UserRole
{
    /// <summary>User biasa dengan akses terbatas</summary>
    User = 0,
    
    /// <summary>Admin divisi - mengelola laporan dalam divisinya</summary>
    AdminDivisi = 1,
    
    /// <summary>Super admin - mengelola semua laporan dan user</summary>
    SuperAdmin = 2,
    
    /// <summary>Super duper admin - akses penuh ke sistem</summary>
    SuperDuperAdmin = 3
}
