using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskUser.Api.Models;

namespace TaskUser.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDb db) : ControllerBase
{

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
        => Ok(await db.Users.AsNoTracking().ToListAsync());

    // GET: api/users/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> GetById(Guid id)
    {
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? NotFound() : Ok(entity);
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<User>> Create([FromBody] CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");

        // uniqueness Name (case-insensitive)
        var exists = await db.Users.AnyAsync(u => u.Name.ToLower() == dto.Name.Trim().ToLower());
        if (exists) return Conflict($"User with Name '{dto.Name}' already exists.");

        var entity = new User { Name = dto.Name.Trim() };
        db.Users.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    // PUT: api/users/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<User>> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");

        var conflict = await db.Users
            .AnyAsync(u => u.Id != id && u.Name.ToLower() == dto.Name.Trim().ToLower());
        if (conflict) return Conflict($"User with Name '{dto.Name}' already exists.");

        entity.Name = dto.Name.Trim();
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        db.Users.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateUserDto(string Name);
public record UpdateUserDto(string Name);