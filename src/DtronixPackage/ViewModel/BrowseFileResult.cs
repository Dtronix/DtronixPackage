namespace DtronixPackage.ViewModel;

public class BrowseFileResult
{
    /// <summary>
    /// True on successful open/selection.  If false, cancel the process
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The path to the file
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Set to true to open the package in a read-only state; False to open normally.
    /// Only used with BrowseOpenFile
    /// </summary>
    public bool OpenReadOnly { get; set; }
}
