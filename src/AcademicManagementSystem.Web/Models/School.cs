namespace AcademicManagementSystem.Web.Models;

public sealed class School
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Programme> Programmes { get; set; } = new List<Programme>();
}