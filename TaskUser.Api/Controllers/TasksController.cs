using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

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
    public async Task<ActionResult<TaskItem>> Create([FromBody] CreateTaskDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("Title is required.");

        if (!Enum.IsDefined(typeof(TaskState), dto.State))
            return BadRequest("Invalid State.");

        // uniqueness Title (case-insensitive)
        var exists = await db.Tasks.AnyAsync(t => t.Title.ToLower() == dto.Title.Trim().ToLower());
        if (exists) return Conflict($"Task with Title '{dto.Title}' already exists.");

        var entity = new TaskItem
        {
            Title = dto.Title.Trim(),
            State = dto.State
        };
        db.Tasks.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    // PUT: api/tasks/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskItem>> Update(Guid id, [FromBody] UpdateTaskDto dto)
    {
        var entity = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("Title is required.");

        if (!Enum.IsDefined(typeof(TaskState), dto.State))
            return BadRequest("Invalid State.");

        var conflict = await db.Tasks
            .AnyAsync(t => t.Id != id && t.Title.ToLower() == dto.Title.Trim().ToLower());
        if (conflict) return Conflict($"Task with Title '{dto.Title}' already exists.");

        entity.Title = dto.Title.Trim();
        entity.State = dto.State;
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    // DELETE: api/tasks/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        db.Tasks.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // (optional) PATCH: api/tasks/{id}/state?state=InProgress
    [HttpPatch("{id:guid}/state")]
    public async Task<ActionResult<TaskItem>> UpdateState(Guid id, [FromQuery] TaskState state)
    {
        var entity = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        if (!Enum.IsDefined(typeof(TaskState), state))
            return BadRequest("Invalid State.");

        entity.State = state;
        await db.SaveChangesAsync();
        return Ok(entity);
    }
}

public record CreateTaskDto(string Title, TaskState State);
public record UpdateTaskDto(string Title, TaskState State);