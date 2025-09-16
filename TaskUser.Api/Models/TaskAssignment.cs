namespace TaskUser.Api.Models
{
    public class TaskAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}