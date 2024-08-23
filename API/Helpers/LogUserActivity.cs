using System;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Helpers;

public class LogUserActivity : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {

        var resultContext = await next();

        if(context.HttpContext.User.Identity?.IsAuthenticated is not true) return;

        var userId = resultContext.HttpContext.User.GetUserId();

        var repo = resultContext.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        var user = await repo.UserRepository.GetUserByIdAsync(userId);

        if(user is null) return;

        user.LastActive = DateTime.UtcNow;
        await repo.Complete();
    }
}
