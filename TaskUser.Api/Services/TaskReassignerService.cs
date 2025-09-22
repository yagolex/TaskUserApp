using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

namespace TaskUser.Api.Services
{
    public class TaskReassignerService(AppDb db, IRandomizer randomizer) : ITaskReassigner
    {
        private const int MaxTasksPerUser = 3;

        public async Task ReassignAsync(CancellationToken ct)
        {
            var activeUsers = await GetActiveUserIdsAsync(ct);
            if (activeUsers.Count == 0) return;

            Dictionary<Guid, int> userLoad = [];
            EnsureAllUsersInLoad(activeUsers, userLoad);

            var tasks = await GetPendingTasksShuffledAsync(ct);     // not Completed, shuffled

            foreach (var task in tasks)
            {
                ct.ThrowIfCancellationRequested();

                var userAssignmentHistory = await GetAssignmentHistoryAsync(task.Id, ct);
                if (HasTaskBeenAssignedToAllUsers(userAssignmentHistory, activeUsers))
                {
                    MarkCompleted(task, userLoad);
                    continue;
                }

                var candidate = PickAssignee(task, activeUsers, userAssignmentHistory, userLoad);
                if (candidate is null)
                {
                    SetWaiting(task); // no slot / no candidates
                    continue;
                }

                ApplyAssignment(task, candidate.Value, userLoad);                
            }

            await db.SaveChangesAsync(ct);
        }

        private Task<List<Guid>> GetActiveUserIdsAsync(CancellationToken ct) =>
            db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);

        private Task<Dictionary<Guid, int>> BuildUserLoadAsync(CancellationToken ct) =>
            db.Tasks
               .Where(t => t.State == TaskState.InProgress && t.CurrentAssigneeId != null)
               .GroupBy(t => t.CurrentAssigneeId!.Value)
               .Select(g => new { g.Key, Count = g.Count() })
               .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        private static void EnsureAllUsersInLoad(List<Guid> users, Dictionary<Guid, int> load)
        {
            foreach (var u in users) if (!load.ContainsKey(u)) load[u] = 0;
        }

        private async Task<List<TaskItem>> GetPendingTasksShuffledAsync(CancellationToken ct)
        {
            var list = await db.Tasks.Where(t => t.State != TaskState.Completed).ToListAsync(ct);
            return list.OrderBy(_ => randomizer.Next(int.MaxValue)).ToList(); // simple shuffle
        }

        private Task<HashSet<Guid>> GetAssignmentHistoryAsync(Guid taskId, CancellationToken ct) =>
            db.TaskAssignments.Where(a => a.TaskId == taskId)
                               .Select(a => a.UserId).Distinct().ToHashSetAsync(ct);

        private Guid? PickAssignee(TaskItem task, List<Guid> users, HashSet<Guid> visited, Dictionary<Guid, int> load)
        {
            var candidates = users.Where(u => SkipPreviousAssignment(u, task) && CanUserAcceptMoreTasks(u, load)).ToList();
            if (candidates.Count == 0) return null;

            var unseen = candidates.Where(u => !visited.Contains(u)).ToList();
            var pool = unseen.Count > 0 ? unseen : candidates;
            return pool[randomizer.Next(pool.Count)];
        }

        private static bool CanUserAcceptMoreTasks(Guid u, Dictionary<Guid, int> load) =>
            load.GetValueOrDefault(u, 0) < MaxTasksPerUser;

        private static bool SkipPreviousAssignment(Guid currentUser, TaskItem task)
        {
            // We should Skip if this is the currently or previously assigned user
            return currentUser != task.CurrentAssigneeId && currentUser != task.PreviousAssigneeId;
        }

        private static void SetWaiting(TaskItem task)
        {
            task.PreviousAssigneeId = task.CurrentAssigneeId;
            task.CurrentAssigneeId = null;
            task.State = TaskState.Waiting;
        }

        private void ApplyAssignment(TaskItem task, Guid userId, Dictionary<Guid, int> load)
        {
            task.PreviousAssigneeId = task.CurrentAssigneeId;
            task.CurrentAssigneeId = userId;
            task.State = TaskState.InProgress;
            load[userId] = load.GetValueOrDefault(userId) + 1;

            db.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = task.Id,
                UserId = userId,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        private static bool HasTaskBeenAssignedToAllUsers(HashSet<Guid> visited, List<Guid> activeUsers)
        {
            return visited.Count >= activeUsers.Count;
        }

        private static void MarkCompleted(TaskItem task, Dictionary<Guid, int> load)
        {
            task.State = TaskState.Completed;
            task.PreviousAssigneeId = task.CurrentAssigneeId;
            task.CurrentAssigneeId = null;
            task.CompletedAt = DateTimeOffset.UtcNow;
            task.UserCountAtCompletion = load.Count;

            // free capacity slot immediately
            Guid lastUser = task.PreviousAssigneeId!.Value;
            if (load[lastUser] > 0)
            {
                load[lastUser] = Math.Max(0, load[lastUser] - 1);
            }
        }       
    }
}