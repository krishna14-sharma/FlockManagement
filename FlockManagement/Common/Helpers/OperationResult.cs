using System.Collections.Generic;

namespace FlockManagement.Common.Helpers
{
    public class OperationResult
    {
        public bool Success { get; protected set; }
        public string Message { get; protected set; }
        public IDictionary<string, string> FieldErrors { get; private set; }

        public OperationResult()
        {
            FieldErrors = new Dictionary<string, string>();
        }

        public static OperationResult Ok(string message = null)
        {
            return new OperationResult { Success = true, Message = message };
        }

        public static OperationResult Fail(string message, string field = null)
        {
            var r = new OperationResult { Success = false, Message = message };
            if (!string.IsNullOrEmpty(field)) r.FieldErrors[field] = message;
            return r;
        }
    }

    public class OperationResult<T> : OperationResult
    {
        public T Data { get; private set; }

        public static OperationResult<T> Ok(T data, string message = null)
        {
            return new OperationResult<T> { Success = true, Data = data, Message = message };
        }

        public static new OperationResult<T> Fail(string message, string field = null)
        {
            var r = new OperationResult<T> { Success = false, Message = message };
            if (!string.IsNullOrEmpty(field)) r.FieldErrors[field] = message;
            return r;
        }
    }
}
