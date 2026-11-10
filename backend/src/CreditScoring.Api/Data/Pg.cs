using Npgsql;

namespace CreditScoring.Api.Data;

public static class Pg
{
    public static bool IsExclusionViolation(Exception ex) => Has(ex, PostgresErrorCodes.ExclusionViolation);

    public static bool IsUniqueViolation(Exception ex) => Has(ex, PostgresErrorCodes.UniqueViolation);

    public static bool IsUniqueViolation(Exception ex, string constraint)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg && pg.ConstraintName == constraint)
                return true;
        }
        return false;
    }

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
