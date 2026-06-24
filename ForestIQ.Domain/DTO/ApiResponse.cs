namespace ForestIQ.Domain.DTO
{
    public class ApiResponse<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Success") 
            => new ApiResponse<T> { Data = data, Success = true, StatusCode = 200, Message = message };

        public static ApiResponse<T> Fail(string message, int statusCode = 500) 
            => new ApiResponse<T> { Data = default, Success = false, StatusCode = statusCode, Message = message };
    }
}
