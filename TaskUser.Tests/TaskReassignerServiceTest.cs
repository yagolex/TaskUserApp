using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

namespace TaskUser.Tests
{
    public class TaskReassignerServiceTest
    {
        [Fact]
        public async Task Should_Assign_From_Waiting_When_Users_Exist()
        {
            using var db = TestHelpers.NewDb(nameof(Should_Assign_From_Waiting_When_Users_Exist));
            db.Users.AddRange(new User { Name = "A" }, new User { Name = "B" });
            var task = new TaskItem { Title = "T1", State = TaskState.Waiting };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db);
            await svc.ReassignAsync(CancellationToken.None);

            var t = await db.Tasks.FindAsync(task.Id);
            t!.State.Should().Be(TaskState.InProgress);
            t.CurrentAssigneeId.Should().NotBeNull();
            t.PreviousAssigneeId.Should().BeNull();
        }

        [Fact]
        public async Task Should_Not_Assign_To_Current_Or_Previous()
        {
            using var db = TestHelpers.NewDb(nameof(Should_Not_Assign_To_Current_Or_Previous));
            var A = new User { Name = "A" }; var B = new User { Name = "B" }; var C = new User { Name = "C" };
            db.Users.AddRange(A, B, C);

            var task = new TaskItem { Title = "T1", State = TaskState.InProgress, CurrentAssigneeId = A.Id, PreviousAssigneeId = B.Id };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db);
            await svc.ReassignAsync(CancellationToken.None);

            var t = await db.Tasks.FindAsync(task.Id);
            // acceptible is C only, otherwise Waiting
            t!.CurrentAssigneeId.Should().Be(C.Id);
            t.PreviousAssigneeId.Should().Be(A.Id);
            t.State.Should().Be(TaskState.InProgress);
        }

        [Fact]
        public async Task Should_Prefer_Unseen_Users()
        {
            using var db = TestHelpers.NewDb(nameof(Should_Prefer_Unseen_Users));
            var A = new User { Name = "A" }; var B = new User { Name = "B" }; var C = new User { Name = "C" };
            db.Users.AddRange(A, B, C);
            var task = new TaskItem { Title = "T1", State = TaskState.Waiting };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db);

            // 1st tic: someone from A/B/C
            await svc.ReassignAsync(CancellationToken.None);
            var t = await db.Tasks.AsNoTracking().FirstAsync();
            var first = t.CurrentAssigneeId!.Value;

            // 2nd tick: current and previous are prohibited (previous is null)
            await svc.ReassignAsync(CancellationToken.None);
            t = await db.Tasks.AsNoTracking().FirstAsync();
            var second = t.CurrentAssigneeId!.Value;

            // 3rd tick: current and previous are prohibited
            await svc.ReassignAsync(CancellationToken.None);
            t = await db.Tasks.AsNoTracking().FirstAsync();
            var third = t.CurrentAssigneeId; // it could become Completed

            var visited = await db.TaskAssignments.Where(a => a.TaskId == task.Id).Select(a => a.UserId).ToListAsync();
            visited.Distinct().Count().Should().BeGreaterThanOrEqualTo(2);
            visited.Should().Contain(first);
            visited.Should().Contain(second);
        }

        [Fact]
        public async Task Should_Complete_When_All_Users_Covered()
        {
            using var db = TestHelpers.NewDb(nameof(Should_Complete_When_All_Users_Covered));
            db.Users.AddRange(new User { Name = "A" }, new User { Name = "B" }, new User { Name = "C" });
            var t1 = new TaskItem { Title = "T1" };
            db.Tasks.Add(t1);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db);
            for (int i = 0; i < 4; i++)
            {
                await svc.ReassignAsync(CancellationToken.None);
            }

            t1.State.Should().Be(TaskState.Completed);
        }

        [Fact]
        public async Task Should_All_Complete_When_All_Users_Covered()
        {
            using var db = TestHelpers.NewDb(nameof(Should_All_Complete_When_All_Users_Covered));
            db.Users.AddRange(new User { Name = "A" }, new User { Name = "B" }, new User { Name = "C" });
            db.Tasks.AddRange(new TaskItem { Title = "T1" }, new TaskItem { Title = "T2" }, new TaskItem { Title = "T3" });
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db);

            int count = 0;
            while (!db.Tasks.All(t => t.CompletedAt.HasValue))
            {
                await svc.ReassignAsync(CancellationToken.None);
                count++;
                if (count > 10)
                    break;
            }

            //Debug.WriteLine("count", count);
            db.Tasks.All(t => t.State == TaskState.Completed).Should().Be(true);
        }

        [Fact]
        public async Task Should_Wait_When_No_Candidates()
        {
            using var db = TestHelpers.NewDb(nameof(Should_Wait_When_No_Candidates));
            var A = new User { Name = "A" };
            db.Users.Add(A);
            var task = new TaskItem { Title = "T1", State = TaskState.InProgress, CurrentAssigneeId = A.Id, PreviousAssigneeId = A.Id };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db);
            await svc.ReassignAsync(CancellationToken.None);

            var t = await db.Tasks.FindAsync(task.Id);
            t!.State.Should().Be(TaskState.Waiting);
            t.CurrentAssigneeId.Should().BeNull();
            t.PreviousAssigneeId.Should().Be(A.Id);
        }

        [Fact]
        public async Task Capacity_Should_Limit_Assignments_To_Three_For_Single_User_In_One_Tick()
        {
            using var db = TestHelpers.NewDb(nameof(Capacity_Should_Limit_Assignments_To_Three_For_Single_User_In_One_Tick));
            var A = new User { Name = "A" };
            var B = new User { Name = "B" };
            db.Users.AddRange(A, B);

            var tasks = Enumerable.Range(1, 7).Select(i => new TaskItem { Title = $"T{i}", State = TaskState.Waiting }).ToList();
            db.Tasks.AddRange(tasks);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db, new TestHelpers.FixedRandomizer());
            await svc.ReassignAsync(CancellationToken.None);

            var inProgress = await db.Tasks.CountAsync(t => t.State == TaskState.InProgress && t.CurrentAssigneeId != null);
            var waiting = await db.Tasks.CountAsync(t => t.State == TaskState.Waiting && t.CurrentAssigneeId == null);

            inProgress.Should().Be(6);
            waiting.Should().Be(1);
        }

        [Fact]
        public async Task Capacity_Should_Limit_Assignments_To_Three_For_Only_User_In_One_Tick()
        {
            using var db = TestHelpers.NewDb(nameof(Capacity_Should_Limit_Assignments_To_Three_For_Only_User_In_One_Tick));
            var A = new User { Name = "A" };
            db.Users.Add(A);

            var tasks = Enumerable.Range(1, 4).Select(i => new TaskItem { Title = $"T{i}", State = TaskState.Waiting }).ToList();
            db.Tasks.AddRange(tasks);
            await db.SaveChangesAsync();

            var svc = TestHelpers.NewService(db, new TestHelpers.FixedRandomizer());
            await svc.ReassignAsync(CancellationToken.None);

            var inProgress = await db.Tasks.CountAsync(t => t.State == TaskState.InProgress && t.CurrentAssigneeId == A.Id);
            var waiting = await db.Tasks.CountAsync(t => t.State == TaskState.Waiting && t.CurrentAssigneeId == null);

            inProgress.Should().Be(3);
            waiting.Should().Be(1);
        }
    }
}