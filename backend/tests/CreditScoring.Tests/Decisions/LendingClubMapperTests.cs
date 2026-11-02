using System.Text.Json;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;

namespace CreditScoring.Tests.Decisions;

public class LendingClubMapperTests
{
    private static List<(string, string)> Table<T>(Func<T, string> toCode) where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => (v.ToString(), toCode(v))).ToList();

    [Fact]
    public void Contract_example_application_maps_to_the_contract_example_request()
    {
        Assert.Equal(TestData.ContractRequest, LendingClubMapper.ToMlRequest(TestData.ContractApplication()));
    }

    [Fact]
    public void Every_field_lands_in_its_own_slot()
    {
        var application = new CreditApplication
        {
            ApplicantName = "Distinct Values",
            SubmittedBy = "tester",
            SubmittedAt = TestData.Now,
            LoanAmount = 35_000,
            Purpose = LoanPurpose.SmallBusiness,
            EmploymentLength = EmploymentLength.TenOrMoreYears,
            HomeOwnership = HomeOwnership.Mortgage,
            AnnualIncome = 123_456.5m,
            IncomeVerification = IncomeVerification.Verified,
            DebtToIncomePercent = 31.2m,
            CreditScore = 742,
            EarliestCreditLine = new DateOnly(1996, 11, 1),
            InquiriesLast6Months = 3,
            DelinquenciesLast2Years = 2,
            PublicRecords = 4,
            Bankruptcies = 1,
            RevolvingBalance = 17_500.25m,
            RevolvingUtilizationPercent = 104.5m,
            OpenAccounts = 14,
            TotalAccounts = 40,
            MortgageAccounts = 5,
        };

        var expected = new MlPredictRequest
        {
            LoanAmnt = 35_000,
            Purpose = "small_business",
            EmpLengthYears = 10,
            HomeOwnership = "MORTGAGE",
            AnnualInc = 123_456.5,
            VerificationStatus = "Verified",
            Dti = 31.2,
            Fico = 742,
            CreditHistoryYears = 10957 / 365.25,
            InqLast6mths = 3,
            Delinq2yrs = 2,
            PubRec = 4,
            PubRecBankruptcies = 1,
            RevolBal = 17_500.25,
            RevolUtil = 104.5,
            OpenAcc = 14,
            TotalAcc = 40,
            MortAcc = 5,
        };

        Assert.Equal(expected, LendingClubMapper.ToMlRequest(application));
    }

    [Fact]
    public void Credit_history_counts_whole_months_as_the_training_data_did()
    {
        var submitted = new DateTimeOffset(2026, 11, 28, 23, 0, 0, TimeSpan.Zero);

        Assert.Equal(4202 / 365.25, LendingClubMapper.CreditHistoryYears(new DateOnly(2015, 5, 31), submitted));
        Assert.Equal(0, LendingClubMapper.CreditHistoryYears(new DateOnly(2026, 11, 2), submitted));
    }

    [Fact]
    public void Credit_history_uses_the_submission_date_in_utc()
    {
        var submitted = new DateTimeOffset(2026, 12, 1, 0, 30, 0, TimeSpan.FromHours(1));

        Assert.Equal(0, LendingClubMapper.CreditHistoryYears(new DateOnly(2026, 11, 1), submitted));
    }

    [Fact]
    public void A_first_credit_line_after_the_submission_month_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LendingClubMapper.CreditHistoryYears(new DateOnly(2026, 12, 1), TestData.Now));
    }

    [Fact]
    public void Employment_length_maps_to_years_and_not_stated_to_null()
    {
        Assert.Equal(
            [null, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
            Enum.GetValues<EmploymentLength>().Select(LendingClubMapper.EmploymentYears).ToList());
    }

    [Fact]
    public void Purpose_codes()
    {
        Assert.Equal(
            [
                ("DebtConsolidation", "debt_consolidation"), ("CreditCard", "credit_card"),
                ("HomeImprovement", "home_improvement"), ("MajorPurchase", "major_purchase"), ("Car", "car"),
                ("Medical", "medical"), ("Moving", "moving"), ("House", "house"), ("Vacation", "vacation"),
                ("Wedding", "wedding"), ("SmallBusiness", "small_business"), ("RenewableEnergy", "renewable_energy"),
                ("Other", "other"),
            ],
            Table<LoanPurpose>(LendingClubMapper.ToCode));
    }

    [Fact]
    public void Home_ownership_codes()
    {
        Assert.Equal(
            [("Rent", "RENT"), ("Own", "OWN"), ("Mortgage", "MORTGAGE"), ("Other", "OTHER")],
            Table<HomeOwnership>(LendingClubMapper.ToCode));
    }

    [Fact]
    public void Income_verification_codes()
    {
        Assert.Equal(
            [("NotVerified", "Not Verified"), ("SourceVerified", "Source Verified"), ("Verified", "Verified")],
            Table<IncomeVerification>(LendingClubMapper.ToCode));
    }

    [Fact]
    public void Undefined_enum_value_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LendingClubMapper.ToCode(default(LoanPurpose)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LendingClubMapper.ToCode((HomeOwnership)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => LendingClubMapper.EmploymentYears((EmploymentLength)99));
    }

    [Fact]
    public void Request_serialises_with_the_contract_field_names_and_keeps_nulls()
    {
        var json = JsonSerializer.Serialize(
            TestData.ContractRequest with { EmpLengthYears = null },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        var names = JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(
            [
                "loan_amnt", "purpose", "emp_length_years", "home_ownership", "annual_inc", "verification_status",
                "dti", "fico", "credit_history_years", "inq_last_6mths", "delinq_2yrs", "pub_rec",
                "pub_rec_bankruptcies", "revol_bal", "revol_util", "open_acc", "total_acc", "mort_acc",
            ],
            names);
        Assert.Contains("\"emp_length_years\":null", json);
    }
}
