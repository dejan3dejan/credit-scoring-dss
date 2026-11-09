using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public static class ApiProblems
{
    public const string CodeKey = "code";

    public const string ValidationFailed = "validation_failed";
    public const string Unauthenticated = "unauthenticated";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string InvalidCredentials = "invalid_credentials";
    public const string LockedOut = "locked_out";
    public const string InferenceUnavailable = "inference_unavailable";

    public static void Configure(ProblemDetailsOptions options) => options.CustomizeProblemDetails = AddDefaultCode;

    public static void AddDefaultCode(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (problem is HttpValidationProblemDetails validation)
            FieldErrors.AddTo(context.HttpContext, validation);
        if (problem.Extensions.ContainsKey(CodeKey))
            return;

        problem.Extensions[CodeKey] = problem.Status switch
        {
            StatusCodes.Status400BadRequest => ValidationFailed,
            StatusCodes.Status401Unauthorized => Unauthenticated,
            StatusCodes.Status403Forbidden => Forbidden,
            StatusCodes.Status404NotFound => NotFound,
            StatusCodes.Status409Conflict => Conflict,
            { } status => $"http_{status}",
            null => throw new InvalidOperationException("A problem without a status cannot get a code."),
        };
    }

    public static ObjectResult Create(
        ControllerBase controller,
        int status,
        string code,
        string title,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, status, title);
        problem.Extensions[CodeKey] = code;
        foreach (var (key, value) in extensions ?? new Dictionary<string, object?>())
            problem.Extensions[key] = value;

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
