using Microsoft.EntityFrameworkCore;
using TaskUser.Api;
using TaskUser.Api.Models;
using TaskUser.Api.Services;

namespace TaskUser.Tests
{
    public static class TestHelpers
    {
        public static AppDb NewDb(string name) =>
            new(new DbContextOptionsBuilder<AppDb>()
                .UseInMemoryDatabase(name)
                .Options);

        public static TaskReassignerService NewService(AppDb db, IRandomizer? rand = null) =>
            new(db, rand ?? new FixedRandomizer());

        public static TaskItem MakeInProgress(string title, Guid userId) =>
            new TaskItem { Title = title, State = TaskState.InProgress, CurrentAssigneeId = userId };

        public class FixedRandomizer : IRandomizer
        {
            private int _i;
            public int Next(int maxExclusive) => (_i++) % Math.Max(1, maxExclusive);
        }
    }
}