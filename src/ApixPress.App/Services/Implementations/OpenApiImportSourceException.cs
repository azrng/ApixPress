namespace ApixPress.App.Services.Implementations;

internal sealed class OpenApiImportSourceException : Exception
{
    public OpenApiImportSourceException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
