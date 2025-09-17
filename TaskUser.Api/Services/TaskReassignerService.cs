using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

namespace TaskUser.Api.Services
{
    public class TaskReassignerService(AppDb db, IRandomizer rand) : ITaskReassigner
    {
        private const int MaxTasksPerUser = 3;

        public async Task ReassignAsync(CancellationToken ct)
        {
            var users = await db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
            if (users.Count == 0) return;

            var userLoad = await db.Tasks
                .Where(t => t.State == TaskState.InProgress && t.CurrentAssigneeId != null)
                .GroupBy(t => t.CurrentAssigneeId!.Value)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

            foreach (var uid in users)
            {
                if (!userLoad.ContainsKey(uid)) userLoad[uid] = 0;
            }

            var tasks = await db.Tasks.Where(t => t.State != TaskState.Completed).ToListAsync(ct);
            tasks = tasks.OrderBy(_ => rand.Next(int.MaxValue)).ToList(); // shaffle

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

                var candidates = users.Where(u => !excluded.Contains(u) &&
                    userLoad.GetValueOrDefault(u, 0) < MaxTasksPerUser).ToList();
                if (candidates.Count == 0)
                {
                    task.PreviousAssigneeId = task.CurrentAssigneeId;
                    task.CurrentAssigneeId = null;
                    task.State = TaskState.Waiting;
                    continue;
                }

                var unseen = candidates.Where(u => !visited.Contains(u)).ToList();
                var pickFrom = unseen.Count > 0 ? unseen : candidates;
                var newUserId = pickFrom[rand.Next(pickFrom.Count)];

                task.PreviousAssigneeId = task.CurrentAssigneeId;
                task.CurrentAssigneeId = newUserId;
                task.State = TaskState.InProgress;

                db.TaskAssignments.Add(new TaskAssignment
                {
                    TaskId = task.Id,
                    UserId = newUserId,
                    AssignedAt = DateTimeOffset.UtcNow
                });

                var newVisited = visited.ToHashSet();
                newVisited.Add(newUserId);
                if (newVisited.Count >= users.Count)
                {
                    task.State = TaskState.Completed;
                    task.PreviousAssigneeId = task.CurrentAssigneeId;
                    task.CurrentAssigneeId = null;

                    userLoad[newUserId] = Math.Max(0, userLoad[newUserId] - 1);
                    
                    task.CompletedAt = DateTimeOffset.UtcNow;
                    task.UserCountAtCompletion = users.Count;
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}