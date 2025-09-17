using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;
using TaskUser.Api.Services;

namespace TaskUser.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController(AppDb db) : ControllerBase
{
    // GET: api/tasks
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetAll()
        => Ok(await db.Tasks.AsNoTracking().ToListAsync());

    // GET: api/tasks/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskItem>> GetById(Guid id)
    {
        var entity = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? NotFound() : Ok(entity);
    }

    // POST: api/tasks
    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create([FromBody] CreateTaskDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("Title is required.");

        // uniqueness Title (case-insensitive)
        var exists = await db.Tasks.AnyAsync(t => t.Title.ToLower() == dto.Title.Trim().ToLower(), cancellationToken);
        if (exists) return Conflict($"Task with Title '{dto.Title}' already exists.");

        var entity = new TaskItem
        {
            Title = dto.Title.Trim()
        };
        db.Tasks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        
        await AssignTaskToAUserAsync(entity, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    private async Task AssignTaskToAUserAsync(TaskItem task, CancellationToken ct)
    {
        var users = await db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct);
        if (users.Count == 0) return;

        var userLoad = await db.Tasks
            .Where(t => t.State == TaskState.InProgress && t.CurrentAssigneeId != null)
            .GroupBy(t => t.CurrentAssigneeId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var candidates = users.Where(u => userLoad.GetValueOrDefault(u, 0) < 3).ToList();
        if (candidates.Count != 0)
        {
            task.CurrentAssigneeId = candidates.FirstOrDefault();
            task.State = TaskState.InProgress;
            await db.SaveChangesAsync(ct);
        }
    }

    // PUT: api/tasks/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskItem>> Update(Guid id, [FromBody] UpdateTaskDto dto)
    {
        var entity = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("Title is required.");

        var conflict = await db.Tasks
            .AnyAsync(t => t.Id != id && t.Title.ToLower() == dto.Title.Trim().ToLower());
        if (conflict) return Conflict($"Task with Title '{dto.Title}' already exists.");

        entity.Title = dto.Title.Trim();

        await db.SaveChangesAsync();
        return Ok(entity);
    }    
}

public record CreateTaskDto(string Title);
public record UpdateTaskDto(string Title);