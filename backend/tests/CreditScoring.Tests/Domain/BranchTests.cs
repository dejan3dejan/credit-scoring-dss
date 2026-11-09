using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class BranchTests
{
    private static Branch NewBranch() => Branch.Create(" ns-01 ", " Novi Sad centar ", " Novi Sad ", TestData.Now);

    [Fact]
    public void A_branch_has_an_upper_case_code_and_trimmed_names()
    {
        var branch = NewBranch();

        Assert.Equal(("NS-01", "Novi Sad centar", "Novi Sad"), (branch.Code, branch.Name, branch.City));
        Assert.True(branch.IsActive);
        Assert.Equal(TestData.Now, branch.CreatedAt);
        Assert.NotEqual(Guid.Empty, branch.Id);
    }

    [Fact]
    public void An_empty_city_is_none()
    {
        Assert.Null(Branch.Create("BG", "Beograd", "  ", TestData.Now).City);
    }

    [Fact]
    public void Code_and_name_are_required()
    {
        Assert.ThrowsAny<ArgumentException>(() => Branch.Create(" ", "Beograd", null, TestData.Now));
        Assert.ThrowsAny<ArgumentException>(() => Branch.Create("BG", " ", null, TestData.Now));
    }

    [Fact]
    public void Editing_changes_name_and_city_but_not_the_code()
    {
        var branch = NewBranch();

        branch.Edit("Novi Sad Liman", null);

        Assert.Equal(("NS-01", "Novi Sad Liman", null), (branch.Code, branch.Name, branch.City));
    }

    [Fact]
    public void A_branch_with_active_users_cannot_be_deactivated()
    {
        var branch = NewBranch();

        var refused = Assert.IsType<Refused>(branch.Deactivate(hasActiveUsers: true));

        Assert.Equal((null, "branch_has_active_users"), (refused.Field, refused.Code));
        Assert.True(branch.IsActive);
    }

    [Fact]
    public void An_empty_branch_is_deactivated_and_reactivated()
    {
        var branch = NewBranch();

        Assert.IsType<Changed>(branch.Deactivate(hasActiveUsers: false));
        Assert.False(branch.IsActive);
        Assert.IsType<Changed>(branch.Deactivate(hasActiveUsers: false));

        branch.Reactivate();
        Assert.True(branch.IsActive);
    }
}
