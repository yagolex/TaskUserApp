using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Services;

namespace TaskUser.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssignmentsController(AppDb db, ITaskReassigner reassigner) : ControllerBase
{
    // GET api/assignments/overview
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var tasks = await db.Tasks
            .Select(t => new
            {
                t.Id,
                t.Title,
                State = t.State,
                CurrentAssigneeId = t.CurrentAssigneeId,
                PreviousAssigneeId = t.PreviousAssigneeId,
                VisitedUserIds = db.TaskAssignments
                    .Where(a => a.TaskId == t.Id)
                    .Select(a => a.UserId)
                    .Distinct()
                    .ToList(),
            })
            .ToListAsync();

        var users = await db.Users.AsNoTracking().ToListAsync();

        return Ok(new
        {
            Users = users.Select(u => new { u.Id, u.Name, u.IsActive }),
            Tasks = tasks
        });
    }

    // GET api/assignments/visited/{taskId}
    [HttpGet("visited/{taskId:guid}")]
    public async Task<IActionResult> Visited(Guid taskId)
    {
        var records = await db.TaskAssignments
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.AssignedAt)
            .Select(a => new { a.UserId, a.AssignedAt })
            .ToListAsync();

        return Ok(records);
    }

    // POST api/assignments/run-once
    [HttpPost("run-once")]
    public async Task<IActionResult> RunOnce(CancellationToken ct)
    {
        await reassigner.ReassignAsync(ct);
        return await Overview();
    }
}
