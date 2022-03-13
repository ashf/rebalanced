using System.Text;

namespace ReBalanced.API.Exceptions;

public class ArgumentsException : Exception
{
    private readonly string[] _paramNames;

    public ArgumentsException(string message, string[] paramNames)
        : base(message)
    {
        _paramNames = paramNames;
    }

    public ArgumentsException(string message, Exception inner, string[] paramNames)
        : base(message, inner)
    {
        _paramNames = paramNames;
    }

    public override string Message
    {
        get
        {
            var sb = new StringBuilder(base.Message);
            sb.AppendLine();
            foreach (var paramName in _paramNames) sb.AppendLine(paramName);
            return sb.ToString();
        }
    }
}