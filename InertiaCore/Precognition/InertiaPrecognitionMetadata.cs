namespace InertiaCore.Precognition;

/// <summary>
/// Endpoint metadata marking an endpoint that opted in with <c>WithInertiaPrecognition</c>.
/// Lets other Inertia filters recognise precognition endpoints, and lets the .NET 10
/// problem-details writer answer a framework validation failure with the Precognition response.
/// </summary>
internal sealed class InertiaPrecognitionMetadata
{
}
