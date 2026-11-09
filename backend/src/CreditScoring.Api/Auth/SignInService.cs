using CreditScoring.Api.Data;
using CreditScoring.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Auth;

public abstract record SignInResult;

public sealed record SignedIn(User User) : SignInResult;

public sealed record InvalidCredentials : SignInResult;

public sealed record LockedOut(DateTimeOffset Until) : SignInResult;

public class SignInService(CreditScoringDbContext db, TimeProvider time)
{
    public async Task<SignInResult> SignInAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var name = User.NormalizeUserName(userName);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == name, cancellationToken);
        var now = time.GetUtcNow();

        if (user is null || !user.IsActive)
        {
            Credentials.VerifyAgainstNobody(password);
            return new InvalidCredentials();
        }
        if (user.IsLockedOut(now))
            return new LockedOut(user.LockedUntil!.Value);

        if (!Credentials.Verify(user, password))
        {
            user.RecordFailedSignIn(now);
            await db.SaveChangesAsync(CancellationToken.None);
            return user.IsLockedOut(now) ? new LockedOut(user.LockedUntil!.Value) : new InvalidCredentials();
        }

        user.RecordSignIn(now);
        await db.SaveChangesAsync(CancellationToken.None);
        return new SignedIn(user);
    }
}
