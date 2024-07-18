using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API;

[ApiController]
[Route("api/[controller]")] // /api.users
public class UsersController(DataContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppUser>>> GetUsers()
    {
        List<AppUser> users = await context.Users.ToListAsync();

        return users;
    }

    [HttpGet("{id:int}")] // /api/users/id
    public async Task<ActionResult<AppUser>> GetUser(int id)
    {
        AppUser? user = await context.Users.FindAsync(id);

        if(user is null) return NotFound();

        return user;
    }
}
