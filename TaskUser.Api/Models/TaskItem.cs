namespace TaskUser.Api.Models
{
    public class TaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = default!;
        public TaskState State { get; set; } = TaskState.Waiting;

        public Guid? CurrentAssigneeId { get; set; }
        public Guid? PreviousAssigneeId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedAt { get; set; }
        
        public int? UserCountAtCompletion { get; set; }
    }
}