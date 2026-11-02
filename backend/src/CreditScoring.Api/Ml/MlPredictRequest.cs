using System.Text.Json.Serialization;

namespace CreditScoring.Api.Ml;

public sealed record MlPredictRequest
{
    [JsonPropertyName("loan_amnt")]
    public required int LoanAmnt { get; init; }

    [JsonPropertyName("purpose")]
    public required string Purpose { get; init; }

    [JsonPropertyName("emp_length_years")]
    public required int? EmpLengthYears { get; init; }

    [JsonPropertyName("home_ownership")]
    public required string HomeOwnership { get; init; }

    [JsonPropertyName("annual_inc")]
    public required double AnnualInc { get; init; }

    [JsonPropertyName("verification_status")]
    public required string VerificationStatus { get; init; }

    [JsonPropertyName("dti")]
    public required double Dti { get; init; }

    [JsonPropertyName("fico")]
    public required double Fico { get; init; }

    [JsonPropertyName("credit_history_years")]
    public required double CreditHistoryYears { get; init; }

    [JsonPropertyName("inq_last_6mths")]
    public required int InqLast6mths { get; init; }

    [JsonPropertyName("delinq_2yrs")]
    public required int Delinq2yrs { get; init; }

    [JsonPropertyName("pub_rec")]
    public required int PubRec { get; init; }

    [JsonPropertyName("pub_rec_bankruptcies")]
    public required int PubRecBankruptcies { get; init; }

    [JsonPropertyName("revol_bal")]
    public required double RevolBal { get; init; }

    [JsonPropertyName("revol_util")]
    public required double RevolUtil { get; init; }

    [JsonPropertyName("open_acc")]
    public required int OpenAcc { get; init; }

    [JsonPropertyName("total_acc")]
    public required int TotalAcc { get; init; }

    [JsonPropertyName("mort_acc")]
    public required int MortAcc { get; init; }
}
