using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IIdentityServerInteractionService interaction,
        IEventService events)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _interaction = interaction;
        _events = events;
    }

    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberLogin { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        // Clear the existing external cookie
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByNameAsync(Username);
        if (user != null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                Username, Password, RememberLogin, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _events.RaiseAsync(new UserLoginSuccessEvent(
                    user.UserName, user.Id, user.UserName));

                // Check if we're in the context of an authorization request
                var context = await _interaction.GetAuthorizationContextAsync(ReturnUrl);

                if (context != null)
                {
                    // We can trust ReturnUrl since GetAuthorizationContextAsync returned non-null
                    return Redirect(ReturnUrl ?? "~/");
                }

                // Request for a local page
                if (Url.IsLocalUrl(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }
                else if (string.IsNullOrEmpty(ReturnUrl))
                {
                    return Redirect("~/");
                }
                else
                {
                    // User might have clicked on a malicious link
                    throw new Exception("Invalid return URL");
                }
            }

            await _events.RaiseAsync(new UserLoginFailureEvent(Username, "Invalid credentials"));
        }

        ErrorMessage = "Invalid username or password";
        return Page();
    }
}
