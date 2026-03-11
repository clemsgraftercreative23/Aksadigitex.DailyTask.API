namespace API.Services;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// Path to Firebase service account JSON file.
    /// If empty, uses GOOGLE_APPLICATION_CREDENTIALS env var.
    /// </summary>
    public string? ServiceAccountPath { get; set; }

    /// <summary>
    /// If true, Firebase push is enabled. If false or path missing, push is skipped.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
