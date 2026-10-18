namespace InertiaCore.Validation;

/// <summary>
/// Endpoint metadata marking an endpoint that opted in with <c>WithInertiaValidation</c>.
/// The .NET 10 problem-details writer uses it to decide whether a framework validation
/// failure on that endpoint should be answered with the Inertia response.
/// </summary>
internal sealed class InertiaValidationMetadata
{
}
