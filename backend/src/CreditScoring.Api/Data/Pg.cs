using Npgsql;

namespace CreditScoring.Api.Data;

public static class Pg
{
    public static bool IsExclusionViolation(Exception ex) => Has(ex, PostgresErrorCodes.ExclusionViolation);

    public static bool IsUniqueViolation(Exception ex) => Has(ex, PostgresErrorCodes.UniqueViolation);

    private static bool Has(Exception? ex, string sqlState)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException pg && pg.SqlState == sqlState)
                return true;
        }
        return false;
    }
}
