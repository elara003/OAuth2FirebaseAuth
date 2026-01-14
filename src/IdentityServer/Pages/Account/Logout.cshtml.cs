using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    public LogoutModel(
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        IEventService events)
    {
        _signInManager = signInManager;
        _interaction = interaction;
        _events = events;
    }

    public string? PostLogoutRedirectUri { get; set; }

    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        // Get context information
        var logout = await _interaction.GetLogoutContextAsync(logoutId);
        PostLogoutRedirectUri = logout?.PostLogoutRedirectUri;

        if (User?.Identity?.IsAuthenticated == true)
        {
            // Delete local authentication cookie
            await _signInManager.SignOutAsync();

            // Raise the logout event
            await _events.RaiseAsync(new UserLogoutSuccessEvent(
                User.GetSubjectId(), User.GetDisplayName()));
        }

        return Page();
    }
}
