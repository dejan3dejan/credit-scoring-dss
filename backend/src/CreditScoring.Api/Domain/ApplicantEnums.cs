namespace CreditScoring.Api.Domain;

public enum CheckingAccountStatus
{
    NoAccount = 1,
    Negative = 2,
    ZeroTo200 = 3,
    AtLeast200OrSalaryAssignment = 4,
}

public enum CreditHistory
{
    PastPaymentDelays = 1,
    CriticalAccount = 2,
    NoCreditsOrAllPaidDuly = 3,
    ExistingPaidDulySoFar = 4,
    AllAtThisBankPaidDuly = 5,
}

public enum LoanPurpose
{
    Other = 1,
    NewCar = 2,
    UsedCar = 3,
    FurnitureOrEquipment = 4,
    RadioOrTelevision = 5,
    DomesticAppliances = 6,
    Repairs = 7,
    Vacation = 8,
    Retraining = 9,
    Business = 10,
}

public enum SavingsAccount
{
    UnknownOrNone = 1,
    Below100 = 2,
    From100To500 = 3,
    From500To1000 = 4,
    AtLeast1000 = 5,
}

public enum EmploymentDuration
{
    Unemployed = 1,
    LessThan1Year = 2,
    From1To4Years = 3,
    From4To7Years = 4,
    AtLeast7Years = 5,
}

public enum PersonalStatusSex
{
    MaleDivorcedOrSeparated = 1,
    FemaleNonSingleOrMaleSingle = 2,
    MaleMarriedOrWidowed = 3,
    FemaleSingle = 4,
}

public enum OtherDebtors
{
    None = 1,
    CoApplicant = 2,
    Guarantor = 3,
}

public enum PropertyType
{
    UnknownOrNone = 1,
    CarOrOther = 2,
    BuildingSocietySavingsOrLifeInsurance = 3,
    RealEstate = 4,
}

public enum OtherInstallmentPlans
{
    Bank = 1,
    Stores = 2,
    None = 3,
}

public enum Housing
{
    ForFree = 1,
    Rent = 2,
    Own = 3,
}

public enum JobType
{
    UnemployedOrUnskilledNonResident = 1,
    UnskilledResident = 2,
    SkilledEmployeeOrOfficial = 3,
    ManagerSelfEmployedOrHighlyQualified = 4,
}
