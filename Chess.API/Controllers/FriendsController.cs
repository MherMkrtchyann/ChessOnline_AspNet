using Chess.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chess.API.Controllers;

public class FriendsController : Controller
{
    private Guid UserId => Guid.Parse(User.FindFirst("Id")!.Value);

    private readonly IUserService _userService;
    private readonly IFriendsService _friendsService;

    public FriendsController(
        IUserService userService,
        IFriendsService friendsService)
    {
        _userService = userService;
        _friendsService = friendsService;
    }

    [HttpPost("send-request")]
    [Authorize]
    public async Task<IActionResult> SendFriendRequest(Guid receiverId)
    {
        var user = await _friendsService.SendRequest(UserId, receiverId);
        return Ok(user);
    }
}