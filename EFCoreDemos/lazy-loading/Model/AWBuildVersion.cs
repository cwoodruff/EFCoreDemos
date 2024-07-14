using System;

namespace lazy_loading.Model;

public class AWBuildVersion
{
    public byte SystemInformationID { get; set; }
    public string Database_Version { get; set; }
    public DateTime VersionDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}