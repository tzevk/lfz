using AssetRequestApi.DTOs;
using AssetRequestApi.Models;
using AssetRequestApi.Seed;
using AssetRequestApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AssetRequestApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtService _jwtService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtService jwtService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [Authorize(Policy = "CanManageSettings")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // Validate role against the allowed 4 roles (case-insensitive match, canonical casing stored)
        var role = SeedData.Roles.FirstOrDefault(
            r => string.Equals(r, dto.Role, StringComparison.OrdinalIgnoreCase));

        if (role is null)
        {
            return BadRequest(new
            {
                message = $"Invalid role '{dto.Role}'. Allowed roles: {string.Join(", ", SeedData.Roles)}"
            });
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            RoleFlag = role,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description) });
        }

        return Ok(new
        {
            message = "User registered successfully.",
            userId = user.Id,
            email = user.Email,
            role
        });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!passwordCheck.Succeeded)
        {
            if (passwordCheck.IsLockedOut)
            {
                return Unauthorized(new { message = "This account is temporarily locked after repeated failed login attempts." });
            }

            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateToken(user, roles);

        return Ok(new
        {
            token,
            userId = user.Id,
            email = user.Email,
            roles
        });
    }
}
