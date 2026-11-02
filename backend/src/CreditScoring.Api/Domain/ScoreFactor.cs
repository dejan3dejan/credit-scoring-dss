namespace CreditScoring.Api.Domain;

public class ScoreFactor
{
    private ScoreFactor() { }

    public Guid Id { get; private set; }
    public Guid CreditDecisionId { get; private set; }
    public int Rank { get; private set; }
    public string Input { get; private set; } = "";
    public double Contribution { get; private set; }

    public static ScoreFactor Of(int rank, string input, double contribution)
    {
        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "ranks start at 1");
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        if (!double.IsFinite(contribution))
            throw new ArgumentOutOfRangeException(nameof(contribution), contribution, "must be finite");

        return new ScoreFactor { Rank = rank, Input = input, Contribution = contribution };
    }
}
