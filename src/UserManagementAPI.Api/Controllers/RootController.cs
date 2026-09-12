using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Hateoas;

namespace UserManagementAPI.Api.Controllers;

/// <summary>
/// The API's front door: a client that knows only this address can discover
/// the rest. Anonymous and version-neutral — it describes the versions rather
/// than belonging to one — and it reveals nothing but addresses; every resource
/// behind them still checks its own permission.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("api")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class RootController(LinkFactory links) : ControllerBase
{
    /// <summary>Links to the top-level resources.</summary>
    [HttpGet(Name = RouteNames.Root)]
    [ProducesResponseType<RootResponse>(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new RootResponse(
    [
        links.Create(HttpContext, RouteNames.Root, "self", HttpMethods.Get),
        links.CreateV1(HttpContext, RouteNames.GetUsers, "users", HttpMethods.Get),
        links.CreateV1(HttpContext, RouteNames.RegisterUser, "create-user", HttpMethods.Post),
        links.CreateV1(HttpContext, RouteNames.GetRoles, "roles", HttpMethods.Get),
        links.CreateV1(HttpContext, RouteNames.Login, "login", HttpMethods.Post)
    ]));
}