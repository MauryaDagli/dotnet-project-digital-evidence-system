using DECMS.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace DECMS.Data;

public class MongoUserStore :
    IUserStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>,
    IQueryableUserStore<ApplicationUser>
{
    private readonly IMongoCollection<ApplicationUser> _users;
    private readonly IMongoCollection<ApplicationRole> _roles;

    public MongoUserStore(MongoDbContext context)
    {
        _users = context.Users;
        _roles = context.Roles;
    }

    public IQueryable<ApplicationUser> Users => _users.AsQueryable();

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _users.DeleteOneAsync(u => u.Id == user.Id, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return await _users.Find(u => u.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await _users.Find(u => u.NormalizedEmail == normalizedEmail).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.NormalizedUserName);
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id);
    }

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.UserName);
    }

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: cancellationToken);
        return IdentityResult.Success;
    }

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.NormalizedEmail);
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
    }

    // roleName coming in here is NORMALIZED (uppercase) from Identity's RoleManager.
    // We look up the actual display name from the roles collection so it's stored as "Admin", not "ADMIN".
    public async Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        // Look up the role by normalized name to get its display name
        var role = await _roles.Find(r => r.NormalizedName == roleName.ToUpperInvariant()).FirstOrDefaultAsync(cancellationToken);
        string displayName = role?.Name ?? roleName; // fallback to whatever was passed

        if (!user.Roles.Any(r => r.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
        {
            user.Roles.Add(displayName);
        }
    }

    public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        user.Roles.RemoveAll(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    // Returns the display-name roles stored in the user document.
    // These are used directly as role claims in the auth cookie, so they MUST match
    // what is used in [Authorize(Roles="Admin,Police Officer")] attributes.
    // We normalize here so that even if the DB has "ADMIN" (uppercase), "Admin" is returned.
    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var displayNames = new List<string>();
        foreach (var stored in user.Roles)
        {
            // Look up the role by normalized name to get the display name
            var upper = stored.ToUpperInvariant();
            var role = await _roles.Find(r => r.NormalizedName == upper).FirstOrDefaultAsync(cancellationToken);
            displayNames.Add(role?.Name ?? stored);
        }
        return displayNames;
    }

    public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        bool isInRole = user.Roles.Any(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(isInRole);
    }

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var filter = Builders<ApplicationUser>.Filter.AnyEq(u => u.Roles, roleName);
        var users = await _users.Find(filter).ToListAsync(cancellationToken);
        return users;
    }

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.SecurityStamp);
    }

    public void Dispose()
    {
    }
}
