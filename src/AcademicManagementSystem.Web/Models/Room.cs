namespace AcademicManagementSystem.Web.Models;

public sealed class Room
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<TimetableEntry> TimetableEntries { get; set; } = new List<TimetableEntry>();
}
