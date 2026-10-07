namespace Presidents.Domain.Common;

/// <summary>Violação de uma regra do domínio histórico.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
