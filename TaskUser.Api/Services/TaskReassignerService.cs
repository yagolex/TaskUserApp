using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

namespace TaskUser.Api.Services
{
    public class TaskReassignerService(ILogger<TaskReassignerService> logger, AppDb db) : ITaskReassigner
    {
        private readonly Random _rng = new();

        public async Task ReassignAsync(CancellationToken ct)
        {
            var users = await db.Users
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            if (users.Count == 0) return;

            var tasks = await db.Tasks
                .Where(t => t.State != TaskState.Completed)
                .ToListAsync(ct);

            tasks = tasks.OrderBy(_ => _rng.Next()).ToList(); // shaffle tasks

            foreach (var task in tasks)
            {
                ct.ThrowIfCancellationRequested();

                var visited = await db.TaskAssignments
                    .Where(a => a.TaskId == task.Id)
                    .Select(a => a.UserId)
                    .Distinct()
                    .ToListAsync(ct);

                var excluded = new HashSet<Guid>();
                if (task.CurrentAssigneeId is Guid cur) excluded.Add(cur);
                if (task.PreviousAssigneeId is Guid prev) excluded.Add(prev);

                var candidates = users.Where(u => !excluded.Contains(u)).ToList();
                if (candidates.Count == 0)
                {
                    // no users available - put task on Waiting
                    task.PreviousAssigneeId = task.CurrentAssigneeId; // set Previous
                    task.CurrentAssigneeId = null;
                    task.State = TaskState.Waiting;
                    continue;
                }

                // start from unvisited first
                var unseen = candidates.Where(u => !visited.Contains(u)).ToList();
                var pickFrom = unseen.Count > 0 ? unseen : candidates;
                var newUserId = pickFrom[_rng.Next(pickFrom.Count)];

                // update assignments
                task.PreviousAssigneeId = task.CurrentAssigneeId;
                task.CurrentAssigneeId = newUserId;
                task.State = TaskState.InProgress;

                db.TaskAssignments.Add(new TaskAssignment
                {
                    TaskId = task.Id,
                    UserId = newUserId,
                    AssignedAt = DateTimeOffset.UtcNow
                });

                // check coverage for all active users
                var newVisitedCount = visited.ToHashSet();
                newVisitedCount.Add(newUserId);

                if (newVisitedCount.Count >= users.Count)
                {
                    // coverage done - Task Completed
                    task.State = TaskState.Completed;
                    task.PreviousAssigneeId = task.CurrentAssigneeId;
                    task.CurrentAssigneeId = null;
                    task.CompletedAt = DateTimeOffset.UtcNow;
                    task.UserCountAtCompletion = users.Count;
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}