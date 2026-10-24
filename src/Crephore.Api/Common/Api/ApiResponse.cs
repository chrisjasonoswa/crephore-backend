namespace Crephore.Api.Common.Api;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }

    // General message (success or high-level info)
    public string? Message { get; set; }

    // Detailed errors
    public Dictionary<string, List<string>>? Errors { get; set; }

    // Success
    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }


    // Fail with single error
    public static ApiResponse<T> Fail(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message ?? "An unexpected error occured.",
            Errors = null
        };
    }


    // Fail with errors
    public static ApiResponse<T> Fail(Dictionary<string, List<string>>? errors, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message ?? "An unexpected error occured.",
            Errors = errors
        };
    }
}
