using System.Text.RegularExpressions;

namespace Crephore.Api.Common.Utilities;

public class ModelValidationHelper
{
    public static string Execute(string input)
    {
        if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
            return input;

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }

    public static string FormatErrorMessage(string errorMessage, string rawFieldName)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return "Invalid input.";

        // Errors about the request as a whole (e.g. a form over its size limit) have no field
        if (string.IsNullOrEmpty(rawFieldName))
            return errorMessage;

        var readableFieldName = ToReadableFieldName(rawFieldName);

        // Replace only the field name, keep rest of message intact
        return errorMessage.Replace(rawFieldName, readableFieldName);
    }

    public static string ToReadableFieldName(string fieldName)
    {
        return Regex
            .Replace(fieldName, "([a-z])([A-Z])", "$1 $2")
            .ToLower();
    }
}
