using System;

namespace FlockManagement.Common.Helpers
{
    /// <summary>
    /// A rule violation whose message is safe to show to the user
    /// (raised by services, or translated from SQL THROW 50000-50999).
    /// </summary>
    [Serializable]
    public class BusinessException : Exception
    {
        public string FieldName { get; private set; }

        public BusinessException(string message) : base(message) { }

        public BusinessException(string fieldName, string message) : base(message)
        {
            FieldName = fieldName;
        }

        public BusinessException(string message, Exception inner) : base(message, inner) { }

        protected BusinessException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
