namespace CreditScoring.Api.Domain;

public enum LoanPurpose
{
    DebtConsolidation = 1,
    CreditCard = 2,
    HomeImprovement = 3,
    MajorPurchase = 4,
    Car = 5,
    Medical = 6,
    Moving = 7,
    House = 8,
    Vacation = 9,
    Wedding = 10,
    SmallBusiness = 11,
    RenewableEnergy = 12,
    Other = 13,
}

public enum EmploymentLength
{
    NotStated = 1,
    LessThanOneYear = 2,
    OneYear = 3,
    TwoYears = 4,
    ThreeYears = 5,
    FourYears = 6,
    FiveYears = 7,
    SixYears = 8,
    SevenYears = 9,
    EightYears = 10,
    NineYears = 11,
    TenOrMoreYears = 12,
}

public enum HomeOwnership
{
    Rent = 1,
    Own = 2,
    Mortgage = 3,
    Other = 4,
}

public enum IncomeVerification
{
    NotVerified = 1,
    SourceVerified = 2,
    Verified = 3,
}
