using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Decisions;

public class StatlogMapperTests
{
    private static List<(string, string)> Table<T>(Func<T, string> toCode) where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => (v.ToString(), toCode(v))).ToList();

    [Fact]
    public void Contract_example_application_maps_to_the_contract_example_request()
    {
        Assert.Equal(TestData.ContractRequest, StatlogMapper.ToMlRequest(TestData.ContractApplication()));
    }

    [Theory]
    [InlineData(100.0, 1)]
    [InlineData(35.0, 1)]
    [InlineData(34.99, 2)]
    [InlineData(25.0, 2)]
    [InlineData(24.99, 3)]
    [InlineData(20.0, 3)]
    [InlineData(19.99, 4)]
    [InlineData(0.0, 4)]
    public void Installment_share_buckets(double share, int code)
    {
        Assert.Equal(code, StatlogMapper.InstallmentRateCode((decimal)share));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(40, 4)]
    public void Residence_buckets(int years, int code)
    {
        Assert.Equal(code, StatlogMapper.ResidenceCode(years));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    [InlineData(9, 4)]
    public void Existing_credits_buckets(int credits, int code)
    {
        Assert.Equal(code, StatlogMapper.ExistingCreditsCode(credits));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(8, 2)]
    public void Dependants_flag(int dependants, int code)
    {
        Assert.Equal(code, StatlogMapper.PeopleLiableCode(dependants));
    }

    [Fact]
    public void Impossible_numeric_inputs_throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatlogMapper.InstallmentRateCode(-0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatlogMapper.ResidenceCode(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatlogMapper.ExistingCreditsCode(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatlogMapper.PeopleLiableCode(-1));
    }

    [Fact]
    public void Undefined_enum_value_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatlogMapper.ToCode(default(CheckingAccountStatus)));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatlogMapper.ToCode((LoanPurpose)11));
    }

    [Fact]
    public void Checking_account_codes()
    {
        Assert.Equal(
            [("NoAccount", "A11"), ("Negative", "A12"), ("ZeroTo200", "A13"), ("AtLeast200OrSalaryAssignment", "A14")],
            Table<CheckingAccountStatus>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Credit_history_codes()
    {
        Assert.Equal(
            [
                ("PastPaymentDelays", "A30"), ("CriticalAccount", "A31"), ("NoCreditsOrAllPaidDuly", "A32"),
                ("ExistingPaidDulySoFar", "A33"), ("AllAtThisBankPaidDuly", "A34"),
            ],
            Table<CreditHistory>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Purpose_codes_skip_A47()
    {
        Assert.Equal(
            [
                ("Other", "A40"), ("NewCar", "A41"), ("UsedCar", "A42"), ("FurnitureOrEquipment", "A43"),
                ("RadioOrTelevision", "A44"), ("DomesticAppliances", "A45"), ("Repairs", "A46"),
                ("Vacation", "A48"), ("Retraining", "A49"), ("Business", "A410"),
            ],
            Table<LoanPurpose>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Savings_codes()
    {
        Assert.Equal(
            [
                ("UnknownOrNone", "A61"), ("Below100", "A62"), ("From100To500", "A63"),
                ("From500To1000", "A64"), ("AtLeast1000", "A65"),
            ],
            Table<SavingsAccount>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Employment_codes()
    {
        Assert.Equal(
            [
                ("Unemployed", "A71"), ("LessThan1Year", "A72"), ("From1To4Years", "A73"),
                ("From4To7Years", "A74"), ("AtLeast7Years", "A75"),
            ],
            Table<EmploymentDuration>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Personal_status_codes()
    {
        Assert.Equal(
            [
                ("MaleDivorcedOrSeparated", "A91"), ("FemaleNonSingleOrMaleSingle", "A92"),
                ("MaleMarriedOrWidowed", "A93"), ("FemaleSingle", "A94"),
            ],
            Table<PersonalStatusSex>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Other_debtors_codes()
    {
        Assert.Equal(
            [("None", "A101"), ("CoApplicant", "A102"), ("Guarantor", "A103")],
            Table<OtherDebtors>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Property_codes()
    {
        Assert.Equal(
            [
                ("UnknownOrNone", "A121"), ("CarOrOther", "A122"),
                ("BuildingSocietySavingsOrLifeInsurance", "A123"), ("RealEstate", "A124"),
            ],
            Table<PropertyType>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Other_installment_plans_codes()
    {
        Assert.Equal(
            [("Bank", "A141"), ("Stores", "A142"), ("None", "A143")],
            Table<OtherInstallmentPlans>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Housing_codes()
    {
        Assert.Equal(
            [("ForFree", "A151"), ("Rent", "A152"), ("Own", "A153")],
            Table<Housing>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Job_codes()
    {
        Assert.Equal(
            [
                ("UnemployedOrUnskilledNonResident", "A171"), ("UnskilledResident", "A172"),
                ("SkilledEmployeeOrOfficial", "A173"), ("ManagerSelfEmployedOrHighlyQualified", "A174"),
            ],
            Table<JobType>(StatlogMapper.ToCode));
    }

    [Fact]
    public void Telephone_and_foreign_worker_codes()
    {
        Assert.Equal("A191", StatlogMapper.TelephoneCode(false));
        Assert.Equal("A192", StatlogMapper.TelephoneCode(true));
        Assert.Equal("A201", StatlogMapper.ForeignWorkerCode(false));
        Assert.Equal("A202", StatlogMapper.ForeignWorkerCode(true));
    }
}
